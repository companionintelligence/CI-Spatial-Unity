using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CompanionScan.Editor
{
    /// <summary>
    /// Release signing and version settings for command-line Android builds. Signing comes from the
    /// environment only, under the names GameCI's unity-builder passes into its build container, so
    /// local builds and CI builds read the same variables. Nothing here creates a keystore, writes a
    /// secret to disk or logs one.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>The APK/AAB is signed with a release key only when all of <see cref="SigningVariables"/>
    /// are set: ANDROID_KEYSTORE_NAME (an existing keystore file; a relative path is resolved against
    /// the project folder, which is where GameCI writes the keystore it decodes from
    /// ANDROID_KEYSTORE_BASE64), ANDROID_KEYSTORE_PASS, ANDROID_KEYALIAS_NAME and ANDROID_KEYALIAS_PASS.
    /// Otherwise Unity signs it with the debug key. A build started with -release fails unless all
    /// four are set.</item>
    /// <item>The version has one source: Player Settings > Version (bundleVersion in
    /// ProjectSettings/ProjectSettings.asset). versionCode is always derived from it as
    /// MAJOR*1000000 + MINOR*1000 + PATCH, so 0.1.0 is 1000 and 1.2.3 is 1002003.</item>
    /// </list>
    /// The build methods apply these settings for one build and put the previous Player Settings
    /// back afterwards, so ProjectSettings.asset never records a keystore path or alias. Unity's
    /// Gradle export still writes both passwords into files under Library/Bee during a signed
    /// build; <see cref="RemoveSigningSecrets"/> deletes those files afterwards.
    /// </remarks>
    public static class AndroidReleaseSettings
    {
        public const string KeystoreNameVariable = "ANDROID_KEYSTORE_NAME";
        public const string KeystorePassVariable = "ANDROID_KEYSTORE_PASS";
        public const string KeyaliasNameVariable = "ANDROID_KEYALIAS_NAME";
        public const string KeyaliasPassVariable = "ANDROID_KEYALIAS_PASS";
        public const string ReleaseArgument = "-release";

        /// <summary>Environment variables that must all be set to sign with a release key.</summary>
        public static readonly IReadOnlyList<string> SigningVariables = new[]
        {
            KeystoreNameVariable, KeystorePassVariable, KeyaliasNameVariable, KeyaliasPassVariable
        };

        /// <summary>
        /// Variables earlier versions of this build read, and what replaced each one. They are no
        /// longer read; a build that finds one set says so.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> RetiredVariables = new Dictionary<string, string>
        {
            { "KEYSTORE_PATH", KeystoreNameVariable },
            { "KEYSTORE_PASS", KeystorePassVariable },
            { "KEY_ALIAS", KeyaliasNameVariable },
            { "KEY_PASS", KeyaliasPassVariable },
            { "VERSION_NAME", "Player Settings > Version (bundleVersion)" },
            { "VERSION_CODE", "Player Settings > Version (bundleVersion), from which versionCode is derived" }
        };

        /// <summary>Highest versionCode Google Play and Meta accept.</summary>
        public const int MaxVersionCode = 2100000000;

        public sealed class Signing
        {
            internal string KeystorePass;
            internal string KeyPass;

            /// <summary>Absolute path of the keystore file, or null when ANDROID_KEYSTORE_NAME is not set.</summary>
            public string KeystorePath { get; internal set; }

            /// <summary>Alias of the release key, or null when ANDROID_KEYALIAS_NAME is not set.</summary>
            public string KeyAlias { get; internal set; }

            /// <summary>Names (never values) of the signing variables that are unset or empty.</summary>
            public IReadOnlyList<string> Missing { get; internal set; } = Array.Empty<string>();

            public bool IsComplete => Missing.Count == 0;

            /// <summary>True when some but not all signing variables are set (probably a mistake).</summary>
            public bool IsPartial => Missing.Count > 0 && Missing.Count < SigningVariables.Count;
        }

        /// <summary>The project folder: the parent of Assets.</summary>
        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        /// <summary>
        /// Reads the signing variables through <paramref name="getVariable"/> (normally the environment).
        /// A relative keystore path is resolved against <paramref name="projectRoot"/>.
        /// </summary>
        public static Signing ReadSigning(Func<string, string> getVariable, string projectRoot)
        {
            string Get(string name) => getVariable(name) is { } value && value.Trim().Length > 0 ? value : null;

            var keystoreName = Get(KeystoreNameVariable);
            var signing = new Signing
            {
                KeystorePath = keystoreName == null ? null : ResolveKeystorePath(keystoreName, projectRoot),
                KeystorePass = Get(KeystorePassVariable),
                KeyAlias = Get(KeyaliasNameVariable),
                KeyPass = Get(KeyaliasPassVariable)
            };
            signing.Missing = SigningVariables.Where(name => Get(name) == null).ToList();
            return signing;
        }

        /// <summary>
        /// Absolute path of the keystore: <paramref name="keystoreName"/> with environment variables
        /// expanded, taken relative to <paramref name="projectRoot"/> unless it is already absolute.
        /// Null when it is not a valid path (<see cref="Validate"/> then fails a signed build).
        /// </summary>
        public static string ResolveKeystorePath(string keystoreName, string projectRoot)
        {
            try
            {
                var path = Environment.ExpandEnvironmentVariables(keystoreName.Trim());
                return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(projectRoot, path));
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is IOException)
            {
                return null;
            }
        }

        /// <summary>Names of <see cref="RetiredVariables"/> that are set.</summary>
        public static IReadOnlyList<string> FindRetiredVariables(Func<string, string> getVariable)
        {
            return RetiredVariables.Keys
                .Where(name => getVariable(name) is { } value && value.Trim().Length > 0)
                .ToList();
        }

        /// <summary>One line naming each retired variable that is set and what replaced it.</summary>
        public static string DescribeRetiredVariables(IEnumerable<string> names)
        {
            return string.Join("; ", names.Select(name => $"{name} is no longer read, use {RetiredVariables[name]}"));
        }

        /// <summary>
        /// Throws <see cref="BuildFailedException"/> when a -release build lacks a complete signing
        /// configuration, or when the keystore file does not exist. Names variables, never values.
        /// </summary>
        public static void Validate(Signing signing, bool release, IReadOnlyList<string> retiredVariablesSet = null)
        {
            if (release && !signing.IsComplete)
            {
                var message = new StringBuilder(
                    $"{ReleaseArgument} needs {string.Join(", ", SigningVariables)} in the environment to sign with the " +
                    $"release key. Missing: {string.Join(", ", signing.Missing)}.");
                if (retiredVariablesSet is { Count: > 0 })
                {
                    message.Append(' ').Append(DescribeRetiredVariables(retiredVariablesSet)).Append('.');
                }

                throw new BuildFailedException(message.ToString());
            }

            // Never quote the path: it can name the user's home folder.
            if (signing.IsComplete && (signing.KeystorePath == null || !File.Exists(signing.KeystorePath)))
            {
                throw new BuildFailedException(
                    $"{KeystoreNameVariable} does not point to an existing keystore file (a relative path is taken " +
                    "from the project folder).");
            }
        }

        /// <summary>
        /// Throws <see cref="BuildFailedException"/> for a -release build while a build profile is
        /// active (<paramref name="activeProfilePath"/> is null when the platform profile is). A build
        /// profile can carry its own copy of Player Settings, as Assets/Settings/Build Profiles/Meta
        /// Quest.asset does, and Unity then reads and writes that copy instead of the project's Player
        /// Settings: the release would take its version and package ID from the profile, and an
        /// interrupted build could leave the keystore path in the profile asset.
        /// </summary>
        public static void CheckActiveBuildProfile(string activeProfilePath, bool release)
        {
            if (release && !string.IsNullOrEmpty(activeProfilePath))
            {
                throw new BuildFailedException(
                    $"{ReleaseArgument} builds use the project's Player Settings, but the build profile " +
                    $"{activeProfilePath} is active. Switch to the Android platform profile (File > Build Profiles > " +
                    "Android > Switch Profile) and build again.");
            }
        }

        /// <summary>MAJOR*1000000 + MINOR*1000 + PATCH for a "MAJOR.MINOR.PATCH" version name.</summary>
        public static int ComputeVersionCode(string versionName)
        {
            var parts = (versionName ?? string.Empty).Trim().Split('.');
            var numbers = new int[3];
            var valid = parts.Length == 3;
            for (var i = 0; valid && i < 3; i++)
            {
                valid = int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]) &&
                        (i == 0 || numbers[i] <= 999);
            }

            long code = valid ? numbers[0] * 1000000L + numbers[1] * 1000L + numbers[2] : 0;
            if (!valid || code < 1 || code > MaxVersionCode)
            {
                throw new BuildFailedException(
                    $"Cannot derive versionCode from version \"{versionName}\": set Player Settings > Version to " +
                    "MAJOR.MINOR.PATCH with MINOR and PATCH at most 999 (versionCode = MAJOR*1000000 + MINOR*1000 + PATCH).");
            }

            return (int)code;
        }

        /// <summary>
        /// Applies the version and signing to Player Settings for one build. Dispose the result to
        /// put the previous values back.
        /// </summary>
        public static IDisposable Apply(string versionName, int versionCode, Signing signing)
        {
            var restore = new PlayerSettingsRestore(signing.IsComplete);
            PlayerSettings.bundleVersion = versionName;
            PlayerSettings.Android.bundleVersionCode = versionCode;
            if (signing.IsComplete)
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = signing.KeystorePath;
                PlayerSettings.Android.keystorePass = signing.KeystorePass;
                PlayerSettings.Android.keyaliasName = signing.KeyAlias;
                PlayerSettings.Android.keyaliasPass = signing.KeyPass;
            }
            else if (PlayerSettings.Android.useCustomKeystore)
            {
                // Without the release variables Unity's debug key signs the build.
                PlayerSettings.Android.useCustomKeystore = false;
            }

            return restore;
        }

        /// <summary>What <see cref="RemoveSigningSecrets"/> found.</summary>
        public sealed class SecretCleanup
        {
            /// <summary>Files deleted because they held a signing password, relative to the searched folder.</summary>
            public List<string> Deleted { get; } = new();

            /// <summary>Files that held a signing password but could not be deleted (for example, locked).</summary>
            public List<string> NotDeleted { get; } = new();

            /// <summary>Files that could not be read, so they were not checked.</summary>
            public List<string> NotChecked { get; } = new();

            public int FilesChecked { get; internal set; }
        }

        /// <summary>
        /// Deletes every file under <paramref name="folder"/> (normally Library/Bee) that contains the
        /// keystore or key password of <paramref name="signing"/>. During a signed build Unity writes
        /// both passwords in plain text into the Gradle project it generates (launcher/build.gradle)
        /// and into Bee's build graph, inputs and logs; it would leave them there, and in any copy or
        /// cache of the Library folder. Unity regenerates the deleted files on the next build.
        /// Only file names are reported, never contents.
        /// </summary>
        public static SecretCleanup RemoveSigningSecrets(Signing signing, string folder)
        {
            var cleanup = new SecretCleanup();
            var patterns = new[] { signing.KeystorePass, signing.KeyPass }
                .Where(secret => !string.IsNullOrEmpty(secret))
                .Distinct()
                .Select(secret => Encoding.UTF8.GetBytes(secret))
                .ToArray();
            if (patterns.Length == 0 || !Directory.Exists(folder))
            {
                return cleanup;
            }

            var root = Path.GetFullPath(folder);
            string Relative(string path) =>
                path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (var file in EnumerateFiles(root, directory => cleanup.NotChecked.Add(Relative(directory) + "/")))
            {
                var relative = Relative(file);
                bool found;
                try
                {
                    found = FileContainsAny(file, patterns);
                    cleanup.FilesChecked++;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    cleanup.NotChecked.Add(relative);
                    continue;
                }

                if (!found)
                {
                    continue;
                }

                try
                {
                    File.Delete(file);
                    cleanup.Deleted.Add(relative);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    cleanup.NotDeleted.Add(relative);
                }
            }

            return cleanup;
        }

        /// <summary>
        /// Every file under <paramref name="root"/>, without following links or junctions. A folder
        /// that cannot be listed is passed to <paramref name="unreadable"/> and skipped.
        /// </summary>
        private static IEnumerable<string> EnumerateFiles(string root, Action<string> unreadable)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                string[] files;
                string[] directories;
                try
                {
                    files = Directory.GetFiles(directory);
                    directories = Directory.GetDirectories(directory);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    unreadable(directory);
                    continue;
                }

                foreach (var file in files)
                {
                    yield return file;
                }

                foreach (var subdirectory in directories)
                {
                    FileAttributes attributes;
                    try
                    {
                        attributes = File.GetAttributes(subdirectory);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                        unreadable(subdirectory);
                        continue;
                    }

                    if ((attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        pending.Push(subdirectory);
                    }
                }
            }
        }

        private const int ScanChunkSize = 1 << 20;

        /// <summary>True when the file contains any of <paramref name="patterns"/>, including across chunk borders.</summary>
        internal static bool FileContainsAny(string path, IReadOnlyList<byte[]> patterns)
        {
            var overlap = patterns.Max(pattern => pattern.Length) - 1;
            var buffer = new byte[ScanChunkSize + overlap];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 1 << 16, FileOptions.SequentialScan);
            var carried = 0;
            while (true)
            {
                var read = stream.Read(buffer, carried, ScanChunkSize);
                if (read <= 0)
                {
                    return false;
                }

                var filled = carried + read;
                var window = new ReadOnlySpan<byte>(buffer, 0, filled);
                foreach (var pattern in patterns)
                {
                    if (window.IndexOf(new ReadOnlySpan<byte>(pattern)) >= 0)
                    {
                        return true;
                    }
                }

                carried = Math.Min(overlap, filled);
                Buffer.BlockCopy(buffer, filled - carried, buffer, 0, carried);
            }
        }

        private sealed class PlayerSettingsRestore : IDisposable
        {
            private readonly bool _restoreKeystore;
            private readonly string _bundleVersion = PlayerSettings.bundleVersion;
            private readonly int _versionCode = PlayerSettings.Android.bundleVersionCode;
            private readonly bool _useCustomKeystore = PlayerSettings.Android.useCustomKeystore;
            private readonly string _keystoreName = PlayerSettings.Android.keystoreName;
            private readonly string _keyaliasName = PlayerSettings.Android.keyaliasName;

            public PlayerSettingsRestore(bool restoreKeystore)
            {
                _restoreKeystore = restoreKeystore;
            }

            public void Dispose()
            {
                PlayerSettings.bundleVersion = _bundleVersion;
                PlayerSettings.Android.bundleVersionCode = _versionCode;
                if (PlayerSettings.Android.useCustomKeystore != _useCustomKeystore)
                {
                    PlayerSettings.Android.useCustomKeystore = _useCustomKeystore;
                }

                // Only after a release-signed build: unsigned builds leave the keystore settings
                // untouched, so they cannot dirty ProjectSettings.asset.
                if (_restoreKeystore)
                {
                    PlayerSettings.Android.keystoreName = _keystoreName;
                    PlayerSettings.Android.keyaliasName = _keyaliasName;
                    PlayerSettings.Android.keystorePass = string.Empty;
                    PlayerSettings.Android.keyaliasPass = string.Empty;
                }
            }
        }
    }
}
