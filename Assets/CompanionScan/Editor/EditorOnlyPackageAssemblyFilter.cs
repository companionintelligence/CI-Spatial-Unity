using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CompanionScan.Editor
{
    /// <summary>
    /// Keeps development-only package assemblies out of player builds.
    ///
    /// <para>
    /// <c>com.coplaydev.unity-mcp</c> (MCP for Unity) is an Editor automation bridge
    /// used while developing this project. Its <c>MCPForUnity.Runtime</c> assembly has
    /// no platform restriction in its asmdef, so without this filter Unity copies it
    /// into every player build (the Quest APK). Nothing in the shipped app uses it.
    /// The package is immutable (it is resolved from git), so the assembly is removed
    /// here, at the <see cref="IFilterBuildAssemblies"/> stage, instead of by editing
    /// its asmdef.
    /// </para>
    /// <para>
    /// The Editor is unaffected: the bridge keeps working in Play Mode and in the
    /// Editor; only player builds drop the assembly.
    /// </para>
    /// </summary>
    public sealed class EditorOnlyPackageAssemblyFilter : IFilterBuildAssemblies
    {
        /// <summary>
        /// Assembly names (or name prefixes followed by '.') that must never ship in a
        /// player. "MCPForUnity" matches MCPForUnity.dll and MCPForUnity.*.dll.
        /// </summary>
        public static readonly string[] ExcludedAssemblyNames = { "MCPForUnity" };

        public int callbackOrder => 0;

        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            var kept = Filter(assemblies, out var removed);
            foreach (var path in removed)
            {
                Debug.Log($"[EditorOnlyPackageAssemblyFilter] Excluded from player build: {Path.GetFileName(path)}");
            }
            return kept;
        }

        /// <summary>Returns the assemblies to keep; <paramref name="removed"/> receives the rest.</summary>
        public static string[] Filter(string[] assemblies, out string[] removed)
        {
            if (assemblies == null)
            {
                removed = Array.Empty<string>();
                return Array.Empty<string>();
            }

            removed = assemblies.Where(IsExcluded).ToArray();
            return assemblies.Where(a => !IsExcluded(a)).ToArray();
        }

        /// <summary>True when the assembly at <paramref name="assemblyPath"/> must not ship.</summary>
        public static bool IsExcluded(string assemblyPath)
        {
            if (string.IsNullOrEmpty(assemblyPath))
            {
                return false;
            }

            var name = Path.GetFileNameWithoutExtension(assemblyPath);
            return ExcludedAssemblyNames.Any(excluded =>
                name.Equals(excluded, StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(excluded + ".", StringComparison.OrdinalIgnoreCase));
        }
    }
}
