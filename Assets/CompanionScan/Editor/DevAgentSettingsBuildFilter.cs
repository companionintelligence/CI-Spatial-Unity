using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CompanionScan.Editor
{
    /// <summary>
    /// Keeps the Meta XR SDK's DevAgentSettings asset out of player builds.
    /// </summary>
    /// <remarks>
    /// Meta XR Core SDK 209 creates <c>Assets/Resources/DevAgentSettings.asset</c> on every editor
    /// load and stores this machine's LAN IP and AgentBridge access token in it, and its
    /// <c>DevAgentBuildProcessor</c> injects both again at build time. Everything under a Resources
    /// folder ships in the player, so every APK would carry them. Companion Scan does not use the
    /// Immersive Debugger AI Assistant, the only runtime reader of the asset, which skips itself
    /// when the asset is missing. The SDK does not recreate the asset while a player is building,
    /// and recreates it on the next editor load.
    /// </remarks>
    public sealed class DevAgentSettingsBuildFilter : IPreprocessBuildWithReport
    {
        public const string AssetPath = "Assets/Resources/DevAgentSettings.asset";

        // Runs after the SDK's DevAgentBuildProcessor (callbackOrder 1).
        public int callbackOrder => 1000;

        public void OnPreprocessBuild(BuildReport report) => RemoveFromProject();

        /// <summary>
        /// Deletes the asset if it exists. The project's build methods also call this before
        /// <see cref="BuildPipeline.BuildPlayer(BuildPlayerOptions)"/>, because Unity skips the
        /// remaining build callbacks, this one included, when another package's callback throws
        /// while Unity creates it. The Meta Voice SDK's ConduitManifestGenerationManager did that
        /// when Assembly-CSharp was loaded twice, which the Meta Immersive Debugger's
        /// CompilationProcessor causes by reloading compiled assemblies from bytes. The Voice SDK
        /// is no longer in the project, but any other callback that throws would do the same.
        /// </summary>
        public static void RemoveFromProject()
        {
            if (!File.Exists(AssetPath))
                return;

            if (!AssetDatabase.DeleteAsset(AssetPath))
                throw new BuildFailedException(
                    $"Could not delete {AssetPath}; the build would ship this machine's LAN IP and AgentBridge access token.");

            Debug.Log($"[CompanionScan] Deleted {AssetPath} so the build does not ship this machine's LAN IP and AgentBridge access token.");
        }
    }
}
