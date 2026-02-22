using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CommandLineBuild
{
    private const string DefaultOutputDir = "Builds/Android";

    [MenuItem("Build/Android/Build Quest APK")]
    public static void BuildAndroidQuestMenu()
    {
        BuildAndroidQuest();
    }

    // Execute via command line: -executeMethod CommandLineBuild.BuildAndroidQuest
    public static void BuildAndroidQuest()
    {
        var outputDir = Path.GetFullPath(DefaultOutputDir);
        Directory.CreateDirectory(outputDir);

        var productName = string.IsNullOrWhiteSpace(PlayerSettings.productName)
            ? "Unity-Android-Player"
            : SanitizeFileName(PlayerSettings.productName);
        var outputPath = Path.Combine(outputDir, $"{productName}.apk");

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

        Debug.Log($"CommandLineBuild: Building APK to {outputPath}");
        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"Build failed: {report.summary.result}");
        }

        Debug.Log("CommandLineBuild: Build succeeded.");
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name.Trim();
    }
}
