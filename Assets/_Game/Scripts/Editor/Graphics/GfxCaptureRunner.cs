using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Presentation.Characters;
using DontTrustYourLuck.Presentation.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DontTrustYourLuck.EditorTools.Graphics
{
    [Flags]
    public enum GfxShots
    {
        None = 0,
        /// <summary>Title screen over the diorama.</summary>
        Menu = 1,
        /// <summary>Match start: every piece on tile 0.</summary>
        Start = 2,
        /// <summary>After a scripted sequence of turns (pieces spread over the board).</summary>
        Mid = 4,
        Showcase = Menu | Start | Mid
    }

    /// <summary>
    /// One step of a capture session: either an edit-mode step (<see cref="EditStep"/>) or a play session at a fixed
    /// Game view size and quality level, wrapped by optional edit-mode <see cref="Setup"/> / <see cref="Teardown"/>.
    /// Teardown always runs once Setup started, also when the session is aborted.
    /// </summary>
    public sealed class GfxCaptureJob
    {
        public string Name;
        public string Note;
        public int Width = 1280;
        public int Height = 720;
        /// <summary>Quality level during the session (0 = Mobile, the WebGL default); -1 keeps the Editor's level.</summary>
        public int QualityLevel = GfxCaptureRunner.WebQualityLevel;
        public GfxShots Shots = GfxShots.Mid;
        public Action Setup;
        public Action Teardown;
        /// <summary>Edit-mode step without a play session (e.g. apply settings between captures).</summary>
        public Action EditStep;
    }

    /// <summary>
    /// Editor-only capture sessions for the graphics phases. Each play job opens GameBoard, sets the Game view to a
    /// fixed size, skips the title screen, plays a seeded all-human match with a deterministic die policy and saves
    /// screenshots, 3x crops (pieces, front tile 3, back tile 27) and render statistics to
    /// Logs/GfxCaptures/&lt;session&gt;_&lt;time&gt;/ (captures.md + log.txt). Statistics come from the Editor
    /// renderer (UnityStats), not from a Web build. Gameplay rules are only driven through the HUD buttons.
    /// </summary>
    [InitializeOnLoad]
    public static class GfxCaptureRunner
    {
        public const int WebQualityLevel = 0;
        public const string GameScenePath = "Assets/_Game/Scenes/GameBoard.unity";
        public const string CaptureRoot = "Logs/GfxCaptures";
        private const int MatchSeed = 2026;
        private const int MidTurns = 16;
        private const int SettleFrames = 30;
        private const int CropScale = 3;
        private const double ShotTimeoutSeconds = 15;
        private const double StepTimeoutSeconds = 120;
        private static readonly int[] CropTiles = { 3, 27 };
        private const float PlaceholderWarnPercent = 1f;

        private enum Step { Idle, Next, Setup, EnteringPlay, WaitMenu, StartMatch, WaitStart, Drive, WaitMid, Shot, ExitingPlay, Teardown }

        private static readonly Queue<GfxCaptureJob> Jobs = new Queue<GfxCaptureJob>();
        private static GfxCaptureJob _job;
        private static Step _step = Step.Idle;
        private static Step _afterShot;
        private static string _dir, _session;
        private static StringBuilder _report, _log;
        private static Action<string, bool> _onComplete;
        private static MatchDirector _director;
        private static int _frames, _turns, _qualityBefore = -1;
        private static bool _setupRan, _failed, _muteBefore, _focusBefore, _exitRequested;
        private static double _stepStart;
        private static string _shotPath, _shotLabel, _shotRow;
        private static readonly List<(string name, Rect rect)> ShotCrops = new List<(string, Rect)>();

        /// <summary>SessionState survives script reloads, the static fields of a session do not.</summary>
        private const string RunningKey = "DontTrustYourLuck.GfxCapture.Session";

        static GfxCaptureRunner()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            string interrupted = SessionState.GetString(RunningKey, "");
            if (interrupted.Length == 0) return;
            SessionState.EraseString(RunningKey);
            Debug.LogError($"[GfxCapture] Session '{interrupted}' was interrupted by a script reload. Leave Play mode and run " +
                           "Tools > Don't Trust Your Luck > Graphics > Restore Pending A-B Changes.");
        }

        public static bool IsRunning => _step != Step.Idle;

        /// <summary>Appends text (e.g. what an edit step changed) to the running session's log.txt.</summary>
        public static void Note(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (string line in text.Split('\n'))
            {
                if (line.Trim().Length > 0) Log("  " + line.TrimEnd('\r'));
            }
        }

        /// <summary>Starts a session. <paramref name="onComplete"/> receives the output folder and success.</summary>
        public static bool Run(string session, IEnumerable<GfxCaptureJob> jobs, Action<string, bool> onComplete = null)
        {
            if (IsRunning)
            {
                Debug.LogWarning("[GfxCapture] A capture session is already running.");
                return false;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[GfxCapture] Leave Play mode before starting a capture session.");
                return false;
            }
            if (!EditorSettings.enterPlayModeOptionsEnabled ||
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0)
            {
                Debug.LogError("[GfxCapture] Needs Enter Play Mode Options without domain reload (Project Settings > Editor): " +
                               "the session lives in static fields.");
                return false;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            _session = session;
            _dir = Path.Combine(CaptureRoot, $"{session}_{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}");
            Directory.CreateDirectory(_dir);
            _report = new StringBuilder();
            _log = new StringBuilder();
            _onComplete = onComplete;
            _failed = false;
            _muteBefore = EditorUtility.audioMasterMute;
            _focusBefore = PlayModeWindow.GetPlayModeFocused();
            EditorUtility.audioMasterMute = true;
            SessionState.SetString(RunningKey, session);
            Jobs.Clear();
            foreach (var job in jobs) Jobs.Enqueue(job);
            Log($"session {session}: {Jobs.Count} steps, Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceType}, " +
                $"seed {MatchSeed}, mid shot after {MidTurns} turns");
            Enter(Step.Next);
            return true;
        }

        // ------------------------------------------------------------------ state machine

        private static void Tick()
        {
            if (_step == Step.Idle) return;
            try
            {
                Advance();
            }
            catch (Exception e)
            {
                Abort("exception in " + _step + ": " + e);
            }
        }

        private static bool InPlaySteps =>
            _step == Step.WaitMenu || _step == Step.StartMatch || _step == Step.WaitStart || _step == Step.Drive ||
            _step == Step.WaitMid || _step == Step.Shot;

        private static double Elapsed => EditorApplication.timeSinceStartup - _stepStart;

        private static void Enter(Step step)
        {
            _step = step;
            _frames = 0;
            _exitRequested = false;
            _stepStart = EditorApplication.timeSinceStartup;
        }

        private static void Advance()
        {
            if (InPlaySteps && !EditorApplication.isPlaying)
            {
                Abort("Play mode stopped during " + _step);
                return;
            }
            if (InPlaySteps && _step != Step.Shot && Elapsed > StepTimeoutSeconds)
            {
                Abort("timed out in " + _step);
                return;
            }

            switch (_step)
            {
                case Step.Next:
                    if (Jobs.Count == 0)
                    {
                        Finish();
                        return;
                    }
                    _job = Jobs.Dequeue();
                    if (_job.EditStep != null)
                    {
                        Log($"step {_job.Name}: {_job.Note}");
                        _job.EditStep();
                        return;
                    }
                    Enter(Step.Setup);
                    return;

                case Step.Setup:
                    if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                    EnsureGameScene();
                    _setupRan = true;
                    _job.Setup?.Invoke();
                    _qualityBefore = QualitySettings.GetQualityLevel();
                    if (_job.QualityLevel >= 0 && _job.QualityLevel != _qualityBefore) QualitySettings.SetQualityLevel(_job.QualityLevel, true);
                    PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
                    PlayModeWindow.SetCustomRenderingResolution((uint)_job.Width, (uint)_job.Height, $"GFX {_job.Width}x{_job.Height}");
                    PlayModeWindow.SetPlayModeFocused(true);
                    Log($"job {_job.Name}: {_job.Width}x{_job.Height}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}" +
                        (string.IsNullOrEmpty(_job.Note) ? "" : $" ({_job.Note})"));
                    EditorApplication.EnterPlaymode();
                    Enter(Step.EnteringPlay);
                    return;

                case Step.EnteringPlay:
                    if (!EditorApplication.isPlaying)
                    {
                        if (Elapsed > StepTimeoutSeconds) Abort("Play mode did not start");
                        return;
                    }
                    _director = Object.FindAnyObjectByType<MatchDirector>();
                    if (_director == null)
                    {
                        if (++_frames > 600) Abort("no MatchDirector in the scene");
                        return;
                    }
                    PlayModeWindow.GetRenderingResolution(out uint w, out uint h);
                    if (w != _job.Width || h != _job.Height) Log($"WARNING Game view renders {w}x{h}, expected {_job.Width}x{_job.Height}");
                    var director = new SerializedObject(_director);
                    var roll = director.FindProperty("rollAnimation"); // cosmetic flicker only: skipped to keep sessions short
                    if (roll != null)
                    {
                        roll.floatValue = 0f;
                        director.ApplyModifiedPropertiesWithoutUndo();
                    }
                    Enter((_job.Shots & GfxShots.Menu) != 0 ? Step.WaitMenu : Step.StartMatch);
                    return;

                case Step.WaitMenu:
                {
                    var menu = _director.Hud != null ? _director.Hud.MainMenu : null;
                    if (menu == null)
                    {
                        Log("no main menu in the HUD: menu shot skipped");
                        Enter(Step.StartMatch);
                        return;
                    }
                    if (!menu.IsVisible || menu.IsAnimating || ShaderUtil.anythingCompiling)
                    {
                        _frames = 0;
                        return;
                    }
                    if (++_frames < SettleFrames) return;
                    RequestShot("menu", Step.StartMatch, false);
                    return;
                }

                case Step.StartMatch:
                {
                    var menu = _director.Hud != null ? _director.Hud.MainMenu : null;
                    if (menu != null && menu.IsVisible) menu.SkipToGame();
                    _director.SetTimings(0.01f, 0.01f, 0.01f, 0f);
                    if (!_director.BeginMatch(HumanSeats(_director), MatchSeed))
                    {
                        Abort("BeginMatch failed");
                        return;
                    }
                    _turns = 0;
                    Enter(Step.WaitStart);
                    return;
                }

                case Step.WaitStart:
                    if (_director.IsBusy || ShaderUtil.anythingCompiling)
                    {
                        _frames = 0;
                        return;
                    }
                    if (++_frames < SettleFrames) return;
                    if ((_job.Shots & GfxShots.Start) != 0) RequestShot("start", Step.Drive, true);
                    else Enter(Step.Drive);
                    return;

                case Step.Drive:
                {
                    if ((_job.Shots & GfxShots.Mid) == 0)
                    {
                        Enter(Step.ExitingPlay);
                        return;
                    }
                    var state = _director.Session.State;
                    if (_turns >= MidTurns || state.IsFinished)
                    {
                        Log($"mid state: round {state.RoundNumber}, lucky {state.LuckyNumber}, unlucky {state.UnluckyNumber}, " +
                            string.Join(", ", state.Players.Select(p => $"P{p.Id}@{p.Position}{(p.Status == PlayerStatus.Active ? "" : " " + p.Status)}")));
                        Enter(Step.WaitMid);
                        return;
                    }
                    if (!_director.IsBusy) DriveOneAction(state);
                    return;
                }

                case Step.WaitMid:
                    if (_director.IsBusy || ShaderUtil.anythingCompiling)
                    {
                        _frames = 0;
                        return;
                    }
                    if (++_frames < SettleFrames) return;
                    RequestShot("mid", Step.ExitingPlay, true);
                    return;

                case Step.Shot:
                    PollShot();
                    return;

                case Step.ExitingPlay:
                    if (EditorApplication.isPlaying)
                    {
                        if (!_exitRequested) EditorApplication.ExitPlaymode();
                        _exitRequested = true;
                        return;
                    }
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    Enter(Step.Teardown);
                    return;

                case Step.Teardown:
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                    RunTeardown();
                    Enter(Step.Next);
                    return;
            }
        }

        private static void Abort(string reason)
        {
            if (_step == Step.Idle)
            {
                Debug.LogError("[GfxCapture] " + reason);
                return;
            }
            _failed = true;
            Log("ERROR " + reason);
            Debug.LogError("[GfxCapture] " + reason);
            Jobs.Clear();
            Enter(EditorApplication.isPlaying ? Step.ExitingPlay : Step.Teardown);
        }

        private static void RunTeardown()
        {
            if (_qualityBefore >= 0 && QualitySettings.GetQualityLevel() != _qualityBefore) QualitySettings.SetQualityLevel(_qualityBefore, true);
            _qualityBefore = -1;
            if (!_setupRan) return;
            _setupRan = false;
            try
            {
                _job?.Teardown?.Invoke();
            }
            catch (Exception e)
            {
                _failed = true;
                Log("ERROR teardown of " + _job?.Name + ": " + e);
                Debug.LogError("[GfxCapture] teardown failed: " + e);
            }
        }

        private static void Finish()
        {
            _step = Step.Idle;
            SessionState.EraseString(RunningKey);
            EditorUtility.audioMasterMute = _muteBefore;
            PlayModeWindow.SetPlayModeFocused(_focusBefore);
            var md = new StringBuilder();
            md.AppendLine($"# Capturas: {_session}");
            md.AppendLine();
            md.AppendLine("Estatísticas do renderizador do Editor (UnityStats, Game view), não de um build Web. " +
                          $"Recortes {CropScale}x por vizinho mais próximo.");
            md.AppendLine();
            md.AppendLine("| Job | Shot | Tela | Qualidade / RP | Render scale | MSAA | Sombra | HDR | Draw calls | SetPass | Agrupadas (est./din./inst.) | Tris | Shadow casters | Renderers ativos | Materiais | Shaders | Arquivo |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            md.Append(_report);
            Log(_failed ? "session FAILED" : "session done");
            try
            {
                File.WriteAllText(Path.Combine(_dir, "captures.md"), md.ToString());
                File.WriteAllText(Path.Combine(_dir, "log.txt"), _log.ToString());
            }
            catch (IOException e)
            {
                _failed = true;
                Debug.LogError("[GfxCapture] could not write the report: " + e.Message);
            }
            Debug.Log($"[GfxCapture] {_session} {(_failed ? "FAILED" : "done")}: {Path.GetFullPath(_dir)}");
            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke(_dir, !_failed);
        }

        private static void Log(string line)
        {
            _log?.AppendLine($"{DateTime.Now:HH:mm:ss} {line}");
        }

        // ------------------------------------------------------------------ scene and match

        private static void EnsureGameScene()
        {
            if (SceneManagerActivePath() == GameScenePath) return;
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        }

        private static string SceneManagerActivePath() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

        /// <summary>The scene's seats (names, colours, animals) with every seat human, so the session drives all turns.</summary>
        private static List<ParticipantConfig> HumanSeats(MatchDirector director)
        {
            var seats = new List<ParticipantConfig>();
            var list = new SerializedObject(director).FindProperty("participants");
            for (int i = 0; list != null && i < list.arraySize; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                seats.Add(new ParticipantConfig(e.FindPropertyRelative("displayName").stringValue,
                    e.FindPropertyRelative("color").colorValue, false,
                    e.FindPropertyRelative("character").objectReferenceValue as CharacterDefinitionAsset));
            }
            return seats;
        }

        private static void DriveOneAction(MatchState state)
        {
            var dice = _director.Hud.DicePanel;
            switch (state.Phase)
            {
                case MatchPhase.AwaitingRoll:
                    dice.RollButton.onClick.Invoke();
                    break;
                case MatchPhase.AwaitingDiceChoice:
                    dice.ChooseButtons[ChooseDie(state)].onClick.Invoke();
                    _turns++;
                    break;
            }
        }

        /// <summary>
        /// Deterministic choice that spreads the pieces and keeps the match going: avoid the unlucky number and the
        /// winning lucky number; odd player ids take the farther destination, even ids the nearer one.
        /// </summary>
        private static int ChooseDie(MatchState state)
        {
            var roll = state.CurrentRoll;
            int id = state.CurrentPlayerId;
            int position = state.GetPlayer(id).Position;
            var movement = new MovementService(_director.Session.Rules);
            int a = Destination(movement, position, roll[0], state.LuckyNumber);
            int b = Destination(movement, position, roll[1], state.LuckyNumber);
            bool okA = a >= 0 && a != state.UnluckyNumber && a != state.LuckyNumber;
            bool okB = b >= 0 && b != state.UnluckyNumber && b != state.LuckyNumber;
            if (okA && okB) return (id % 2 == 1) == (a >= b) ? 0 : 1;
            if (okA) return 0;
            return okB ? 1 : 0;
        }

        private static int Destination(MovementService movement, int position, int steps, int lucky)
        {
            try
            {
                return movement.Move(position, steps, lucky).FinalPosition;
            }
            catch (Exception)
            {
                return -1; // invalid preview: the die is simply not preferred
            }
        }

        // ------------------------------------------------------------------ shots

        private static void RequestShot(string label, Step next, bool crops)
        {
            _shotLabel = label;
            _shotPath = Path.GetFullPath(Path.Combine(_dir, $"{_job.Name}_{label}.png"));
            if (File.Exists(_shotPath)) File.Delete(_shotPath);
            _shotRow = StatsRow(label);
            CollectCrops(crops);
            ScreenCapture.CaptureScreenshot(_shotPath, 1);
            _afterShot = next;
            Enter(Step.Shot);
        }

        private static void PollShot()
        {
            bool written = File.Exists(_shotPath) && new FileInfo(_shotPath).Length > 0;
            if (!written)
            {
                if (Elapsed > ShotTimeoutSeconds)
                {
                    Log($"WARNING screenshot {_job.Name}_{_shotLabel} timed out (is the Game view visible?)");
                    Enter(_afterShot);
                }
                return;
            }
            if (++_frames < 3) return; // the PNG is written at the end of a frame; give the writer a moment
            ProcessShot();
            Enter(_afterShot);
        }

        private static void ProcessShot()
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!image.LoadImage(File.ReadAllBytes(_shotPath)))
                {
                    Log("WARNING could not decode " + _shotPath);
                    return;
                }
                if (image.width != _job.Width || image.height != _job.Height)
                    Log($"WARNING {_job.Name}_{_shotLabel}: {image.width}x{image.height} instead of {_job.Width}x{_job.Height}");
                var pixels = image.GetPixels32();
                foreach (var (name, rect) in ShotCrops)
                {
                    string cropPath = Path.Combine(_dir, $"{_job.Name}_{_shotLabel}_{name}.png");
                    SaveCrop(pixels, image.width, image.height, rect, cropPath);
                }
                _report.AppendLine(_shotRow + $" {Path.GetFileName(_shotPath)} |");
                int placeholder = PlaceholderPixels(pixels);
                float share = 100f * placeholder / pixels.Length;
                Log($"shot {_job.Name}_{_shotLabel}: {image.width}x{image.height}, {ShotCrops.Count} crops, " +
                    $"placeholder-cyan {placeholder} px ({share.ToString("0.###", CultureInfo.InvariantCulture)}%)");
                if (share > PlaceholderWarnPercent)
                {
                    _failed = true;
                    Log($"WARNING {_job.Name}_{_shotLabel} looks like shaders still compiling (cyan placeholders): shot not valid");
                    Debug.LogWarning($"[GfxCapture] {_job.Name}_{_shotLabel}: {share:0.#}% cyan placeholder pixels");
                }
            }
            finally
            {
                Object.DestroyImmediate(image);
            }
        }

        /// <summary>
        /// Pixels in the colour of the Editor's async-shader placeholder (cyan, also dimmed under the menu overlay).
        /// The menu's tile border alone gives about 0.15%; F1 pipeline shots taken while shaders compiled gave 38-42%.
        /// </summary>
        private static int PlaceholderPixels(Color32[] pixels)
        {
            int n = 0;
            foreach (var c in pixels)
            {
                if (c.g > 80 && c.r * 3 < c.g && Mathf.Abs(c.g - c.b) * 6 < c.g) n++;
            }
            return n;
        }

        private static void CollectCrops(bool enabled)
        {
            ShotCrops.Clear();
            var cam = Camera.main;
            if (!enabled || cam == null || _director == null) return;
            foreach (var piece in _director.ActivePieces)
            {
                if (piece == null || !piece.isActiveAndEnabled) continue;
                Vector3 bottom = cam.WorldToScreenPoint(piece.transform.position);
                Vector3 top = cam.WorldToScreenPoint(piece.transform.position + Vector3.up * 1.05f);
                if (bottom.z <= 0f) continue;
                float h = Mathf.Max(40f, top.y - bottom.y);
                ShotCrops.Add(($"piece{piece.PlayerId}", new Rect(bottom.x - h * 0.75f, bottom.y - h * 0.3f, h * 1.5f, h * 1.5f)));
            }
            foreach (int tile in CropTiles)
            {
                if (tile >= _director.Board.TileCount) continue;
                Vector3 anchor = _director.Board.GetTileAnchor(tile).position;
                Vector3 c = cam.WorldToScreenPoint(anchor);
                Vector3 e = cam.WorldToScreenPoint(anchor + Vector3.right * 0.8f);
                if (c.z <= 0f) continue;
                float half = Mathf.Max(30f, Mathf.Abs(e.x - c.x));
                ShotCrops.Add(($"tile{tile:00}", new Rect(c.x - half, c.y - half * 0.8f, half * 2f, half * 1.6f)));
            }
        }

        private static void SaveCrop(Color32[] src, int srcW, int srcH, Rect r, string path)
        {
            int x0 = Mathf.Clamp(Mathf.RoundToInt(r.x), 0, srcW - 1);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(r.y), 0, srcH - 1);
            int w = Mathf.Clamp(Mathf.RoundToInt(r.width), 1, srcW - x0);
            int h = Mathf.Clamp(Mathf.RoundToInt(r.height), 1, srcH - y0);
            int dw = w * CropScale, dh = h * CropScale;
            var dst = new Color32[dw * dh];
            for (int y = 0; y < dh; y++)
            {
                int sy = y0 + y / CropScale;
                for (int x = 0; x < dw; x++) dst[y * dw + x] = src[sy * srcW + x0 + x / CropScale];
            }
            var crop = new Texture2D(dw, dh, TextureFormat.RGBA32, false);
            try
            {
                crop.SetPixels32(dst);
                crop.Apply(false);
                File.WriteAllBytes(path, crop.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(crop);
            }
        }

        private static string StatsRow(string label)
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            string rpName = rp != null ? rp.name : "built-in";
            float scale = 1f;
            int msaa = 1, shadowRes = 0, softQuality = 0;
            bool soft = false, hdr = false;
            if (rp != null)
            {
                var so = new SerializedObject(rp);
                scale = Float(so, "m_RenderScale", 1f);
                msaa = Int(so, "m_MSAA", 1);
                shadowRes = Int(so, "m_MainLightShadowmapResolution", 0);
                soft = Int(so, "m_SoftShadowsSupported", 0) != 0;
                softQuality = Int(so, "m_SoftShadowQuality", 0);
                hdr = Int(so, "m_SupportsHDR", 0) != 0;
            }
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            var materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            int shaders = materials.Select(m => m.shader).Where(s => s != null).Distinct().Count();
            PlayModeWindow.GetRenderingResolution(out uint w, out uint h);
            string shadow = shadowRes == 0 ? "-" : $"{shadowRes} {(soft ? "soft q" + softQuality : "hard")}";
            return string.Format(CultureInfo.InvariantCulture,
                "| {0} | {1} | {2}x{3} | {4} / {5} | {6:0.##} | {7}x | {8} | {9} | {10} | {11} | {12} | {13:0.0}k | {14} | {15} | {16} | {17} |",
                _job.Name, label, w, h, QualitySettings.names[QualitySettings.GetQualityLevel()], rpName, scale, msaa, shadow,
                hdr ? "on" : "off", UnityStats.drawCalls, UnityStats.setPassCalls,
                $"{UnityStats.staticBatchedDrawCalls}/{UnityStats.dynamicBatchedDrawCalls}/{UnityStats.instancedBatchedDrawCalls}",
                UnityStats.triangles / 1000f, UnityStats.shadowCasters, renderers.Length, materials.Length, shaders);
        }

        private static int Int(SerializedObject so, string name, int fallback)
        {
            var p = so.FindProperty(name);
            if (p == null) return fallback;
            return p.propertyType == SerializedPropertyType.Boolean ? (p.boolValue ? 1 : 0) : p.intValue;
        }

        private static float Float(SerializedObject so, string name, float fallback)
        {
            var p = so.FindProperty(name);
            return p != null ? p.floatValue : fallback;
        }
    }
}
