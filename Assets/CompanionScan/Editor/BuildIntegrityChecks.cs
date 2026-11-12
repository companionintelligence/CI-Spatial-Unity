using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CompanionScan.Editor
{
    /// <summary>
    /// Checks that stop a player build which silently lost build callbacks or Burst code.
    /// </summary>
    /// <remarks>
    /// Before a build, Unity creates one instance of every build-callback class
    /// (<c>BuildPipelineInterfaces.InitializeBuildCallbacks</c>). When a constructor throws,
    /// Unity logs the exception, registers none of the callbacks it had not reached yet, and
    /// still finishes the build. On the Unity 6000.6 upgrade branch the Meta Voice SDK's
    /// <c>ConduitManifestGenerationManager</c> threw there ("An item with the same key has
    /// already been added. Key: Assembly-CSharp", after the Meta Immersive Debugger reloaded
    /// Assembly-CSharp from bytes). One of the callbacks it cut off was the editor's
    /// <c>BurstAOTCompilerPostprocessor</c> (an <c>IGenerateNativePluginsForAssemblies</c>,
    /// part of the editor since Burst moved into Unity 6000.6), so the APK had no
    /// <c>lib_burst_generated.so</c> and every Burst job, URP's included, ran as plain IL2CPP code.
    /// The OpenXR build hooks that drop disabled features' native libraries were cut off too.
    /// The Voice SDK is gone, and these checks make any repeat fail the build instead.
    /// </remarks>
    public static class BuildIntegrityChecks
    {
        public const string BurstLibraryName = "lib_burst_generated.so";
        public const string BurstAndroidSettingsPath = "ProjectSettings/BurstAotSettings_Android.json";

        /// <summary>
        /// Creates every build-callback class the way Unity does before a build and throws
        /// <see cref="BuildFailedException"/> naming each one whose constructor throws.
        /// </summary>
        public static void VerifyBuildCallbacksConstruct()
        {
            var failures = FindThrowingBuildCallbacks();
            if (failures.Count == 0)
            {
                Debug.Log("[BuildChecks] Every build callback can be created.");
                return;
            }

            throw new BuildFailedException(
                "Build callbacks throw while Unity creates them. Unity would skip every callback after the first " +
                "failure, Burst compilation and the DevAgentSettings filter included, and still report success:\n  " +
                string.Join("\n  ", failures));
        }

        /// <summary>Build-callback classes whose public parameterless constructor throws, with the reason.</summary>
        public static List<string> FindThrowingBuildCallbacks()
        {
            var failures = new List<string>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IOrderedCallback>())
            {
                if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters ||
                    type.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                try
                {
                    Activator.CreateInstance(type);
                }
                catch (Exception e)
                {
                    var cause = e is TargetInvocationException { InnerException: not null } tie ? tie.InnerException : e;
                    failures.Add($"{type.FullName} ({type.Assembly.GetName().Name}): {cause.GetType().Name}: {cause.Message}");
                }
            }

            return failures;
        }

        /// <summary>True unless ProjectSettings turns Burst AOT compilation off for Android.</summary>
        public static bool IsBurstEnabledForAndroid()
        {
            if (!File.Exists(BurstAndroidSettingsPath))
            {
                return true; // Burst's default.
            }

            var match = Regex.Match(File.ReadAllText(BurstAndroidSettingsPath),
                "\"EnableBurstCompilation\"\\s*:\\s*(true|false)");
            return !match.Success || match.Groups[1].Value == "true";
        }

        /// <summary>Android ABI folder names for the architectures in Player Settings.</summary>
        public static List<string> AndroidAbis()
        {
            var abis = new List<string>();
            var architectures = PlayerSettings.Android.targetArchitectures;
            if ((architectures & AndroidArchitecture.ARM64) != 0) abis.Add("arm64-v8a");
            if ((architectures & AndroidArchitecture.ARMv7) != 0) abis.Add("armeabi-v7a");
            return abis;
        }

        /// <summary>
        /// Throws <see cref="BuildFailedException"/> unless the APK or AAB at
        /// <paramref name="packagePath"/> holds Burst-compiled code for every ABI in
        /// <paramref name="abis"/> (APK: lib/&lt;abi&gt;/, AAB: base/lib/&lt;abi&gt;/).
        /// </summary>
        public static void VerifyBurstCode(string packagePath, IEnumerable<string> abis)
        {
            var missing = FindAbisWithoutBurstCode(packagePath, abis);
            if (missing.Count > 0)
            {
                throw new BuildFailedException(
                    $"{Path.GetFileName(packagePath)} has no {BurstLibraryName} for {string.Join(", ", missing)}, " +
                    $"although Burst AOT compilation is on in {BurstAndroidSettingsPath}. Unity probably skipped the " +
                    "Burst build callback; look for an exception from BuildPipelineInterfaces.InitializeBuildCallbacks " +
                    "in the build log.");
            }

            Debug.Log($"[BuildChecks] {Path.GetFileName(packagePath)} contains {BurstLibraryName} for {string.Join(", ", abis)}.");
        }

        /// <summary>The ABIs in <paramref name="abis"/> without Burst-compiled code in the package.</summary>
        public static List<string> FindAbisWithoutBurstCode(string packagePath, IEnumerable<string> abis)
        {
            HashSet<string> entries;
            using (var stream = File.OpenRead(packagePath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                entries = new HashSet<string>(archive.Entries.Select(entry => entry.FullName));
            }

            return abis
                .Where(abi => !entries.Contains($"lib/{abi}/{BurstLibraryName}") &&
                              !entries.Contains($"base/lib/{abi}/{BurstLibraryName}"))
                .ToList();
        }
    }
}
