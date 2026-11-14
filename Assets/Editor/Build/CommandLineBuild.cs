using System;
using System.IO;
using System.Linq;
using CompanionScan.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Batch-mode Quest build: -executeMethod CommandLineBuild.BuildAndroidQuest
/// </summary>
/// <remarks>
/// Optional arguments and environment variables (see README, "Release builds from the command line"):
/// <list type="bullet">
/// <item>-release: the build must be signed with the release key; fails unless KEYSTORE_PATH,
/// KEYSTORE_PASS, KEY_ALIAS and KEY_PASS are all set.</item>
/// <item>-aab: build an Android App Bundle (Builds/Android/&lt;product&gt;.aab) instead of an APK.</item>
/// <item>KEYSTORE_PATH, KEYSTORE_PASS, KEY_ALIAS, KEY_PASS: when all four are set the build is signed
/// with that key, otherwise with Unity's debug key.</item>
/// <item>VERSION_CODE: versionCode; default MAJOR*1000000 + MINOR*1000 + PATCH of the version name.</item>
/// <item>VERSION_NAME: version name; default Player Settings' bundleVersion.</item>
/// </list>
/// Signing and version settings apply to this build only; Player Settings are put back afterwards.
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
        var signing = AndroidReleaseSettings.ReadSigning(Environment.GetEnvironmentVariable);
        AndroidReleaseSettings.Validate(signing, release);
        var versionName = AndroidReleaseSettings.ResolveVersionName(Environment.GetEnvironmentVariable, PlayerSettings.bundleVersion);
        var versionCode = AndroidReleaseSettings.ResolveVersionCode(Environment.GetEnvironmentVariable, versionName);

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
            Debug.Log($"CommandLineBuild: Signing with the release key from {AndroidReleaseSettings.KeystorePathVariable}.");
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
