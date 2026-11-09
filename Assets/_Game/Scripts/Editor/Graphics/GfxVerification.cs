using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace DontTrustYourLuck.EditorTools.Graphics
{
    /// <summary>
    /// Runs the EditMode then the PlayMode tests and writes one summary file (failures with messages). The callback
    /// runs back in Edit mode with true when both runs had no failures.
    /// </summary>
    public static class GfxTestChain
    {
        private static TestRunnerApi _api;
        private static Chain _chain;

        public static void Run(string summaryPath, Action<bool> done)
        {
            if (_chain != null)
            {
                Debug.LogWarning("[GfxTests] A test chain is already running.");
                return;
            }
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _chain = new Chain(summaryPath, done);
            _api.RegisterCallbacks(_chain);
            _chain.Execute(TestMode.EditMode);
        }

        private sealed class Chain : ICallbacks
        {
            private readonly string _path;
            private readonly Action<bool> _done;
            private readonly StringBuilder _text = new StringBuilder();
            private TestMode _mode;
            private bool _ok = true;

            public Chain(string path, Action<bool> done)
            {
                _path = path;
                _done = done;
            }

            public void Execute(TestMode mode)
            {
                _mode = mode;
                _api.Execute(new ExecutionSettings(new Filter { testMode = mode }));
            }

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                _ok &= result.FailCount == 0 && result.PassCount > 0;
                _text.AppendLine($"{_mode}: PASS={result.PassCount} FAIL={result.FailCount} SKIP={result.SkipCount} " +
                                 $"INCONCLUSIVE={result.InconclusiveCount}");
                AppendProblems(result, _text);
                File.WriteAllText(_path, _text.ToString());
                Debug.Log($"[GfxTests] {_mode}: {result.PassCount} passed, {result.FailCount} failed -> {Path.GetFullPath(_path)}");
                if (_mode == TestMode.EditMode)
                {
                    EditorApplication.delayCall += () => Execute(TestMode.PlayMode);
                    return;
                }
                _api.UnregisterCallbacks(this);
                _chain = null;
                bool ok = _ok;
                WhenInEditMode(() => _done?.Invoke(ok));
            }

            private static void AppendProblems(ITestResultAdaptor r, StringBuilder sb)
            {
                if (r.HasChildren)
                {
                    foreach (var child in r.Children) AppendProblems(child, sb);
                    return;
                }
                if (r.TestStatus == TestStatus.Failed) sb.AppendLine($"  FAILED {r.FullName}: {r.Message}");
                else if (r.TestStatus == TestStatus.Skipped) sb.AppendLine($"  skipped {r.FullName}: {r.Message}");
            }
        }

        private static void WhenInEditMode(Action action)
        {
            void Wait()
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                EditorApplication.update -= Wait;
                action();
            }
            EditorApplication.update += Wait;
        }
    }

    /// <summary>
    /// Builds the Web player with the 'Web - Desktop - Development' build profile to Builds/Web (ignored by Git) and
    /// writes a summary: result, size, time, errors/warnings and the canvas size written to index.html. If the build
    /// rewrites the profile asset (seen in Sprint 4), the file is restored.
    /// </summary>
    public static class GfxWebBuild
    {
        public const string OutputDir = "Builds/Web";
        public const string ProfilePath = "Assets/Settings/Build Profiles/Web - Desktop - Development.asset";

        [MenuItem("Tools/Don't Trust Your Luck/Graphics/Build Web (Development profile)")]
        public static void BuildMenu() => Build(null);

        public static bool Build(string reportDir)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogError("[GfxWebBuild] Missing build profile " + ProfilePath);
                return false;
            }
            string profileText = File.ReadAllText(ProfilePath);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = OutputDir,
                options = BuildOptions.None // Development comes from the profile
            });
            var s = report.summary;
            var sb = new StringBuilder();
            sb.AppendLine($"profile: {ProfilePath}");
            sb.AppendLine($"result: {s.result}, platform {s.platform}, output {s.outputPath}");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "size: {0:N0} bytes, time: {1:hh\\:mm\\:ss}, errors: {2}, warnings: {3}",
                s.totalSize, s.totalTime, s.totalErrors, s.totalWarnings));
            if (File.ReadAllText(ProfilePath) != profileText)
            {
                File.WriteAllText(ProfilePath, profileText);
                AssetDatabase.ImportAsset(ProfilePath, ImportAssetOptions.ForceUpdate);
                sb.AppendLine("the build rewrote the profile asset: restored");
            }
            string index = Path.Combine(OutputDir, "index.html");
            if (File.Exists(index))
            {
                var m = Regex.Match(File.ReadAllText(index), @"width=""?(\d+)""?\s+height=""?(\d+)""?");
                sb.AppendLine("index.html canvas: " + (m.Success ? $"{m.Groups[1].Value}x{m.Groups[2].Value}" : "not found"));
            }
            foreach (var step in report.steps)
            {
                foreach (var message in step.messages)
                {
                    if (message.type == LogType.Error || message.type == LogType.Exception || message.type == LogType.Warning)
                        sb.AppendLine($"{message.type}: {message.content}");
                }
            }
            string dir = reportDir ?? GfxCaptureRunner.CaptureRoot;
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"webbuild_{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.txt");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[GfxWebBuild] {s.result}: {Path.GetFullPath(path)}");
            return s.result == BuildResult.Succeeded;
        }
    }
}
