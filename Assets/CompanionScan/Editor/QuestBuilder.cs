using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Linq;

namespace CompanionScan.Editor
{
    /// <summary>
    /// Command-line build helper for Quest 3 APK builds.
    /// Invoke via:
    ///   Unity.exe -quit -batchmode -projectPath . -executeMethod CompanionScan.Editor.QuestBuilder.Build
    /// Or from the Unity menu: CompanionScan â†’ Build Quest APK
    /// </summary>
    public static class QuestBuilder
    {
        private const string ApkPath = "Builds/Quest/CompanionScan.apk";

        [MenuItem("CompanionScan/Build Quest APK")]
        public static void Build()
        {
            // Get enabled scenes from build settings
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[QuestBuilder] No scenes enabled in Build Settings!");
                return;
            }

            Debug.Log($"[QuestBuilder] Building {scenes.Length} scene(s) to {ApkPath}");
            foreach (var s in scenes)
                Debug.Log($"  Scene: {s}");

            // Ensure output directory exists
            var dir = System.IO.Path.GetDirectoryName(ApkPath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[QuestBuilder] âœ“ Build succeeded: {summary.totalSize / (1024 * 1024):F1} MB, {summary.totalTime.TotalSeconds:F1}s");
            }
            else
            {
                Debug.LogError($"[QuestBuilder] âœ— Build failed: {summary.result} ({summary.totalErrors} errors)");
            }
        }

        [MenuItem("CompanionScan/Build and Run Quest APK")]
        public static void BuildAndRun()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[QuestBuilder] No scenes enabled in Build Settings!");
                return;
            }

            var dir = System.IO.Path.GetDirectoryName(ApkPath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            Debug.Log($"[QuestBuilder] Building + running {scenes.Length} scene(s)â€¦");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.AutoRunPlayer
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[QuestBuilder] âœ“ Build + deploy succeeded: {summary.totalSize / (1024 * 1024):F1} MB");
            }
            else
            {
                Debug.LogError($"[QuestBuilder] âœ— Build failed: {summary.result}");
            }
        }
    }
}

