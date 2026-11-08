using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace DontTrustYourLuck.EditorTools
{
    /// <summary>
    /// Runs the project's tests from code and writes a plain-text summary of every test run (also runs started
    /// from the Test Runner window) to Temp/TestRun_results.txt. Callbacks are re-registered after each domain
    /// reload, so PlayMode runs are reported too.
    /// Lets automation (Unity MCP, -executeMethod) read results without opening the Test Runner window.
    /// Menu: Tools > Don't Trust Your Luck > Run EditMode Tests / Run PlayMode Tests
    /// </summary>
    [InitializeOnLoad]
    public static class TestRunnerBridge
    {
        public const string ResultPath = "Temp/TestRun_results.txt";
        private static TestRunnerApi _api;

        static TestRunnerBridge()
        {
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _api.RegisterCallbacks(new Callbacks(ResultPath));
        }

        [MenuItem("Tools/Don't Trust Your Luck/Run EditMode Tests")]
        public static void RunEditMode() => Run(TestMode.EditMode);

        [MenuItem("Tools/Don't Trust Your Luck/Run PlayMode Tests")]
        public static void RunPlayMode() => Run(TestMode.PlayMode);

        private static void Run(TestMode mode)
        {
            if (File.Exists(ResultPath)) File.Delete(ResultPath);
            _api.Execute(new ExecutionSettings(new Filter { testMode = mode }));
        }

        private sealed class Callbacks : ICallbacks
        {
            private readonly string _path;
            public Callbacks(string path) => _path = path;

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"PASS={result.PassCount} FAIL={result.FailCount} SKIP={result.SkipCount} INCONCLUSIVE={result.InconclusiveCount}");
                Append(result, sb);
                File.WriteAllText(_path, sb.ToString());
                Debug.Log($"[TestRunnerBridge] {_path}: {result.PassCount} passed, {result.FailCount} failed.");
            }

            private static void Append(ITestResultAdaptor r, StringBuilder sb)
            {
                if (r.HasChildren)
                {
                    foreach (var child in r.Children) Append(child, sb);
                    return;
                }
                if (r.TestStatus != TestStatus.Passed)
                    sb.AppendLine($"{r.TestStatus} {r.FullName}: {r.Message}\n{r.StackTrace}");
            }
        }
    }
}
