using System;
using System.IO;
using System.Linq;
using CompanionScan.Editor;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Batch-mode Quest build: -executeMethod CommandLineBuild.BuildAndroidQuest. CI builds call it
/// too, through GameCI's buildMethod input.
/// </summary>
/// <remarks>
/// Optional arguments and environment variables (see README, "Release builds from the command line"):
/// <list type="bullet">
/// <item>-release: the build must be signed with the release key; fails unless ANDROID_KEYSTORE_NAME,
/// ANDROID_KEYSTORE_PASS, ANDROID_KEYALIAS_NAME and ANDROID_KEYALIAS_PASS are all set, and while a
/// build profile is active.</item>
/// <item>-aab: build an Android App Bundle (Builds/Android/&lt;product&gt;.aab) instead of an APK.</item>
/// <item>ANDROID_KEYSTORE_NAME (keystore file, absolute or relative to the project folder),
/// ANDROID_KEYSTORE_PASS, ANDROID_KEYALIAS_NAME, ANDROID_KEYALIAS_PASS: when all four are set the
/// build is signed with that key, otherwise with Unity's debug key. These are the names GameCI's
/// unity-builder passes into its build container.</item>
/// </list>
/// The version is Player Settings > Version (bundleVersion), and versionCode is
/// MAJOR*1000000 + MINOR*1000 + PATCH of it. Signing and version settings apply to this build only;
/// Player Settings are put back afterwards.
/// </remarks>
public static class CommandLineBuild
{
    private const string DefaultOutputDir = "Builds/Android";

    /// <summary>Command-line argument that builds an Android App Bundle instead of an APK.</summary>
    public const string AppBundleArgument = "-aab";

    [MenuItem("Build/Android/Build Quest APK")]
    public static void BuildAndroidQuestMenu()
    {
        BuildAndroidQuest();
    }

    // Execute via command line: -executeMethod CommandLineBuild.BuildAndroidQuest [-release] [-aab]
    public static void BuildAndroidQuest()
    {
        var release = HasArgument(AndroidReleaseSettings.ReleaseArgument);
        var appBundle = HasArgument(AppBundleArgument);
        var projectRoot = AndroidReleaseSettings.ProjectRoot;
        var signing = AndroidReleaseSettings.ReadSigning(Environment.GetEnvironmentVariable, projectRoot);
        var retiredVariables = AndroidReleaseSettings.FindRetiredVariables(Environment.GetEnvironmentVariable);
        if (retiredVariables.Count > 0)
        {
            Debug.LogWarning("CommandLineBuild: " + AndroidReleaseSettings.DescribeRetiredVariables(retiredVariables) + ".");
        }

        AndroidReleaseSettings.Validate(signing, release, retiredVariables);

        var activeProfile = BuildProfile.GetActiveBuildProfile();
        var activeProfilePath = activeProfile == null
            ? null
            : AssetDatabase.GetAssetPath(activeProfile) is { Length: > 0 } assetPath ? assetPath : activeProfile.name;
        AndroidReleaseSettings.CheckActiveBuildProfile(activeProfilePath, release);
        if (!string.IsNullOrEmpty(activeProfilePath))
        {
            Debug.LogWarning($"CommandLineBuild: The build profile {activeProfilePath} is active, so its Player Settings apply.");
        }

        var versionName = PlayerSettings.bundleVersion?.Trim();
        var versionCode = AndroidReleaseSettings.ComputeVersionCode(versionName);

        var outputDir = Path.GetFullPath(DefaultOutputDir);
        Directory.CreateDirectory(outputDir);

        var productName = string.IsNullOrWhiteSpace(PlayerSettings.productName)
            ? "Unity-Android-Player"
            : SanitizeFileName(PlayerSettings.productName);
        var packageKind = appBundle ? "AAB" : "APK";
        var outputPath = Path.Combine(outputDir, $"{productName}.{(appBundle ? "aab" : "apk")}");

        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled scenes found in Build Settings.");
        }

        var buildTarget = BuildTarget.Android;
        var buildTargetGroup = BuildTargetGroup.Android;

        if (EditorUserBuildSettings.activeBuildTarget != buildTarget)
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(buildTargetGroup, buildTarget))
            {
                throw new InvalidOperationException("Failed to switch build target to Android.");
            }
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = buildTarget,
            options = BuildOptions.None
        };

        // Keep the machine-specific Meta DevAgentSettings out of the APK even if Unity skips build callbacks.
        DevAgentSettingsBuildFilter.RemoveFromProject();

        // Unity skips the remaining build callbacks (Burst AOT among them) when one throws while it
        // creates them, and still reports success: fail before building instead.
        BuildIntegrityChecks.VerifyBuildCallbacksConstruct();

        if (signing.IsComplete)
        {
            // Names only: never log the keystore path, alias or passwords.
            Debug.Log($"CommandLineBuild: Signing with the release key from {AndroidReleaseSettings.KeystoreNameVariable}.");
        }
        else if (signing.IsPartial)
        {
            Debug.LogWarning("CommandLineBuild: Signing with the debug key because these are not set: " +
                             string.Join(", ", signing.Missing) + ".");
        }
        else
        {
            Debug.Log("CommandLineBuild: Signing with the debug key (no release signing variables set).");
        }

        Debug.Log($"CommandLineBuild: Building {packageKind} {versionName} (versionCode {versionCode}) to {outputPath}");
        BuildReport report;
        var previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
        try
        {
            EditorUserBuildSettings.buildAppBundle = appBundle;
            using (AndroidReleaseSettings.Apply(versionName, versionCode, signing))
            {
                report = BuildPipeline.BuildPlayer(options);
            }
        }
        finally
        {
            EditorUserBuildSettings.buildAppBundle = previousBuildAppBundle;
            if (signing.IsComplete)
            {
                RemoveSigningSecretsFromLibrary(signing, projectRoot);
            }
        }

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"Build failed: {report.summary.result}");
        }

        if (BuildIntegrityChecks.IsBurstEnabledForAndroid())
        {
            BuildIntegrityChecks.VerifyBurstCode(outputPath, BuildIntegrityChecks.AndroidAbis());
        }

        Debug.Log("CommandLineBuild: Build succeeded.");
    }

    /// <summary>
    /// Deletes the files in which Unity's Gradle export left the signing passwords (Library/Bee).
    /// Never throws: a failure here must not hide the build result.
    /// </summary>
    private static void RemoveSigningSecretsFromLibrary(AndroidReleaseSettings.Signing signing, string projectRoot)
    {
        const string folder = "Library/Bee";
        try
        {
            var started = DateTime.UtcNow;
            var cleanup = AndroidReleaseSettings.RemoveSigningSecrets(signing, Path.Combine(projectRoot, folder));
            Debug.Log($"CommandLineBuild: Checked {cleanup.FilesChecked} files in {folder} for the signing passwords in " +
                      $"{(DateTime.UtcNow - started).TotalSeconds:F0} s and deleted {cleanup.Deleted.Count}" +
                      (cleanup.Deleted.Count == 0 ? "." : ": " + string.Join(", ", cleanup.Deleted)));
            if (cleanup.NotDeleted.Count > 0 || cleanup.NotChecked.Count > 0)
            {
                Debug.LogWarning($"CommandLineBuild: Delete {folder} before you copy, share or cache the Library folder. " +
                                 (cleanup.NotDeleted.Count > 0
                                     ? "These files hold a signing password but could not be deleted: " +
                                       string.Join(", ", cleanup.NotDeleted) + ". "
                                     : string.Empty) +
                                 (cleanup.NotChecked.Count > 0
                                     ? "These could not be read: " + string.Join(", ", cleanup.NotChecked) + "."
                                     : string.Empty));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"CommandLineBuild: Could not check {folder} for the signing passwords ({e.GetType().Name}). " +
                           $"Delete {folder} before you copy, share or cache the Library folder.");
        }
    }

    private static bool HasArgument(string name)
    {
        return Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
    }

    // Drops spaces too: "Companion Scan" builds CompanionScan.apk, the name the editor menu build
    // and CI use, and shell commands need no quotes.
    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return new string(name.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }
}
