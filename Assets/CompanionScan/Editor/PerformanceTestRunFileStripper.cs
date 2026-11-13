using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CompanionScan.Editor
{
    /// <summary>
    /// Keeps the Performance Testing run files out of player builds.
    ///
    /// <para>
    /// <c>com.unity.test-framework.performance</c> is not one of our dependencies; it
    /// comes in through <c>com.unity.collections</c>. Its <c>TestRunBuilder</c> is an
    /// <see cref="IPreprocessBuildWithReport"/> (callback order 0) that writes
    /// <c>Assets/Resources/PerformanceTestRunInfo.json</c> and
    /// <c>Assets/Resources/PerformanceTestRunSettings.json</c> at the start of
    /// <em>every</em> player build and deletes them when the build ends. Because the
    /// files sit in a <c>Resources</c> folder, Unity packs them into the player.
    /// <c>PerformanceTestRunInfo</c> lists every package in the project, Editor-only
    /// tools included, plus the Editor version and build settings.
    /// </para>
    /// <para>
    /// This callback runs after <c>TestRunBuilder</c> and removes both files again,
    /// before Unity collects the <c>Resources</c> folders. Player test runs still get
    /// them: the Test Runner builds those players with
    /// <see cref="BuildOptions.IncludeTestAssemblies"/>, and the performance tests
    /// read the files at run time.
    /// </para>
    /// </summary>
    public sealed class PerformanceTestRunFileStripper : IPreprocessBuildWithReport
    {
        /// <summary>Files that <c>TestRunBuilder.OnPreprocessBuild</c> writes.</summary>
        public static readonly string[] GeneratedFiles =
        {
            "Assets/Resources/PerformanceTestRunInfo.json",
            "Assets/Resources/PerformanceTestRunSettings.json",
        };

        /// <summary>After <c>TestRunBuilder</c>, whose callback order is 0.</summary>
        public int callbackOrder => 1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (IsTestBuild(report.summary.options))
            {
                return;
            }

            foreach (var path in RemoveGeneratedFiles())
            {
                Debug.Log($"[PerformanceTestRunFileStripper] Removed {path} from the player build.");
            }
        }

        /// <summary>True for players that the Test Runner builds to run tests on a device.</summary>
        public static bool IsTestBuild(BuildOptions options)
        {
            return (options & BuildOptions.IncludeTestAssemblies) != 0;
        }

        /// <summary>
        /// Deletes the generated files (and their .meta files) that exist, through the
        /// AssetDatabase when Unity has already imported them. Returns the deleted paths.
        /// </summary>
        public static IReadOnlyList<string> RemoveGeneratedFiles()
        {
            var removed = new List<string>();
            foreach (var path in GeneratedFiles)
            {
                var imported = !string.IsNullOrEmpty(
                    AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets));
                if (imported && AssetDatabase.DeleteAsset(path))
                {
                    removed.Add(path);
                    continue;
                }

                var deleted = false;
                if (File.Exists(path))
                {
                    File.Delete(path);
                    deleted = true;
                }

                if (File.Exists(path + ".meta"))
                {
                    File.Delete(path + ".meta");
                    deleted = true;
                }

                if (deleted)
                {
                    removed.Add(path);
                }
            }

            return removed;
        }
    }
}
