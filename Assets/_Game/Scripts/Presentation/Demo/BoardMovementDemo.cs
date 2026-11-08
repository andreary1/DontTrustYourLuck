using System;
using System.Collections.Generic;
using System.Text;
using DontTrustYourLuck.Domain;
using DontTrustYourLuck.Infrastructure;
using DontTrustYourLuck.Presentation.Board;
using DontTrustYourLuck.Presentation.Config;
using UnityEngine;

namespace DontTrustYourLuck.Presentation.Demo
{
    /// <summary>
    /// TECHNICAL DEMO ONLY (Sprint 1). Feeds Inspector-defined moves through the domain services and shows
    /// the result on the board. Not a gameplay system: no turns, input, win or elimination.
    /// Use the component context menu "Run Demo" (in Play Mode or Edit Mode) after changing values.
    /// </summary>
    public sealed class BoardMovementDemo : MonoBehaviour
    {
        [Serializable]
        public sealed class DemoMove
        {
            [Min(0)] public int startPosition;
            [Tooltip("Die value used when 'Roll Dice' is off.")]
            [Min(1)] public int dieValue = 1;

            public DemoMove() { }
            public DemoMove(int start, int die) { startPosition = start; dieValue = die; }
        }

        [SerializeField] private MatchConfigAsset config;
        [SerializeField] private BoardView board;
        [SerializeField] private BoardCameraRig cameraRig;
        [SerializeField] private PlayerPieceView[] pieces = Array.Empty<PlayerPieceView>();

        [Header("Randomness")]
        [SerializeField] private int seed = 2026;
        [Tooltip("Use a fixed lucky number instead of drawing one (30 reproduces the 28 + 5 -> 27 example).")]
        [SerializeField] private bool useFixedLuckyNumber = true;
        [SerializeField] private int fixedLuckyNumber = 30;

        [Header("Moves (one per piece, by array order)")]
        [Tooltip("When off, pieces only stand on their start positions.")]
        [SerializeField] private bool applyMovement = true;
        [Tooltip("Roll the configured dice and use die 0 instead of the fixed die values.")]
        [SerializeField] private bool rollDice = false;
        [SerializeField] private DemoMove[] moves =
        {
            new DemoMove(4, 5),   // normal: 4 -> 9
            new DemoMove(28, 5),  // rebound: 29 30 29 28 27
            new DemoMove(25, 2),  // shares tile 27 with P2
            new DemoMove(26, 4),  // exact landing on 30
        };

        [SerializeField] private bool showOverlay = true;

        private string _report = string.Empty;

        public string Report => _report;

        public void Configure(MatchConfigAsset configAsset, BoardView boardView, BoardCameraRig rig, PlayerPieceView[] pieceViews)
        {
            config = configAsset;
            board = boardView;
            cameraRig = rig;
            pieces = pieceViews;
        }

        private void Start() => Run();

        [ContextMenu("Run Demo")]
        public void Run()
        {
            if (board == null)
            {
                Debug.LogWarning("[BoardMovementDemo] No BoardView assigned.", this);
                return;
            }

            MatchRulesConfig rules;
            try
            {
                rules = config != null ? config.ToRulesConfig() : MatchRulesConfig.Default;
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning($"[BoardMovementDemo] Invalid config: {e.Message}", this);
                return;
            }

            board.EnsureBuilt(rules.TileCount);

            IRandomSource random = new SeededRandomSource(seed);
            var luck = new LuckNumberService(random, rules);
            var dice = new DiceService(random, rules);
            var movement = new MovementService(rules);

            var sb = new StringBuilder();
            int lucky;
            int unlucky;
            try
            {
                // Draw order (documented for reproducibility): lucky (unless fixed), unlucky, then dice per piece.
                lucky = useFixedLuckyNumber ? fixedLuckyNumber : luck.DrawLuckyNumber();
                unlucky = luck.DrawUnluckyNumber(lucky);
            }
            catch (ArgumentOutOfRangeException e)
            {
                Debug.LogWarning($"[BoardMovementDemo] {e.Message}", this);
                return;
            }

            board.SetHighlights(lucky, unlucky);
            sb.AppendLine($"Seed {seed} | Lucky {lucky} | Unlucky {unlucky}");

            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                if (piece == null) continue;
                var move = i < moves.Length ? moves[i] : new DemoMove(0, 1);

                if (!applyMovement)
                {
                    PlaceSafely(piece, move.startPosition, rules, sb);
                    continue;
                }

                int steps = rollDice ? dice.Roll()[0] : move.dieValue;
                try
                {
                    var result = movement.Move(move.startPosition, steps, lucky);
                    piece.SetLogicalPosition(result.FinalPosition);
                    sb.AppendLine($"P{piece.PlayerId}: {result}");
                }
                catch (ArgumentOutOfRangeException e)
                {
                    sb.AppendLine($"P{piece.PlayerId}: invalid move ({move.startPosition} +{steps}): {e.ParamName}");
                    Debug.LogWarning($"[BoardMovementDemo] P{piece.PlayerId}: {e.Message}", this);
                    PlaceSafely(piece, move.startPosition, rules, sb);
                }
            }

            board.PlacePieces(new List<PlayerPieceView>(pieces));
            if (cameraRig != null) cameraRig.Frame();

            _report = sb.ToString();
            Debug.Log("[BoardMovementDemo]\n" + _report, this);
        }

        private static void PlaceSafely(PlayerPieceView piece, int position, MatchRulesConfig rules, StringBuilder sb)
        {
            int clamped = Mathf.Clamp(position, 0, rules.BoardLength);
            piece.SetLogicalPosition(clamped);
            sb.AppendLine($"P{piece.PlayerId}: placed at {clamped}");
        }

        private GUIStyle _style;

        private void OnGUI()
        {
            if (!showOverlay || string.IsNullOrEmpty(_report)) return;
            _style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = Mathf.Max(12, Screen.height / 45),
                wordWrap = true
            };
            float width = Mathf.Min(Screen.width - 20f, 560f);
            GUI.Box(new Rect(10f, 10f, width, _style.fontSize * 7.5f), "SPRINT 1 DEMO\n" + _report, _style);
        }
    }
}
