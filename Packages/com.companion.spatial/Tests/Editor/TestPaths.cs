// Where the tests find the package's own files: the schema in Documentation~ and the golden v1 file in Fixtures~.
// Both folders end in "~", so Unity does not import them.

using System;
using System.IO;

namespace Companion.Spatial.Tests
{
    internal static class TestPaths
    {
        /// <summary>The package folder: from the package manager in the Editor, otherwise from COMPANION_SPATIAL_PACKAGE or the project's Packages folder.</summary>
        internal static string PackageRoot
        {
            get
            {
#if UNITY_EDITOR
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Vec3).Assembly);
                if (info != null && Directory.Exists(info.resolvedPath))
                {
                    return info.resolvedPath;
                }
#endif
                string fromEnvironment = Environment.GetEnvironmentVariable("COMPANION_SPATIAL_PACKAGE");
                if (!string.IsNullOrEmpty(fromEnvironment) && Directory.Exists(fromEnvironment))
                {
                    return fromEnvironment;
                }

                string relative = Path.GetFullPath(Path.Combine("Packages", "com.companion.spatial"));
                if (Directory.Exists(relative))
                {
                    return relative;
                }

                throw new DirectoryNotFoundException("Cannot find the com.companion.spatial package folder (looked for " + relative + ").");
            }
        }

        /// <summary>The golden extras v1 file of the scanner's characterization tests (Golden/extras-v1.json), copied unchanged.</summary>
        internal static string GoldenV1Path
        {
            get { return Path.Combine(PackageRoot, "Tests", "Editor", "Fixtures~", "extras-v1.golden.json"); }
        }

        /// <summary>The hand-written JSON Schema of extras v2.</summary>
        internal static string SchemaPath
        {
            get { return Path.Combine(PackageRoot, "Documentation~", "scan-extras.v2.schema.json"); }
        }

        internal static string ReadGoldenV1()
        {
            return File.ReadAllText(GoldenV1Path);
        }

        internal static string ReadSchema()
        {
            return File.ReadAllText(SchemaPath);
        }
    }
}
