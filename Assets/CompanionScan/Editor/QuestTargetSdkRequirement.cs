using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CompanionScan.Editor
{
    /// <summary>
    /// Fails any Android build whose target API level is not exactly 34.
    /// </summary>
    /// <remarks>
    /// Meta requires Quest apps created after 1 March 2026 to target Android API level 34, and
    /// API levels above 34 are outside what Horizon OS accepts for immersive apps. Player Settings and
    /// the Meta Quest build profile both say 34, but "Automatic (highest installed)" would pick 36 on a
    /// machine with a newer SDK, so every Android build checks it: CommandLineBuild, QuestBuilder, the
    /// Build Profiles window and GameCI's build method alike.
    /// </remarks>
    public sealed class QuestTargetSdkRequirement : IPreprocessBuildWithReport
    {
        public const int RequiredTargetSdk = 34;

        // Early, so a wrong setting fails before anything slow runs.
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android)
            {
                Verify(PlayerSettings.Android.targetSdkVersion);
            }
        }

        /// <summary>Throws <see cref="BuildFailedException"/> unless <paramref name="targetSdk"/> is API level 34.</summary>
        public static void Verify(AndroidSdkVersions targetSdk)
        {
            if ((int)targetSdk != RequiredTargetSdk)
            {
                var actual = targetSdk == AndroidSdkVersions.AndroidApiLevelAuto
                    ? "Automatic (highest installed)"
                    : ((int)targetSdk).ToString();
                throw new BuildFailedException(
                    $"Quest builds must target Android API level {RequiredTargetSdk} exactly, but Player Settings > " +
                    $"Android > Target API Level is {actual}. Set it to API level {RequiredTargetSdk}.");
            }

            Debug.Log($"[QuestTargetSdk] Target API level {RequiredTargetSdk}.");
        }
    }
}
