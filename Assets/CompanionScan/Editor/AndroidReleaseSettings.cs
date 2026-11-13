using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace CompanionScan.Editor
{
    /// <summary>
    /// Release signing and version settings for command-line Android builds, read from the
    /// environment only. Nothing here creates a keystore, writes a secret to disk or logs one.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>The APK/AAB is signed with a release key only when all of <see cref="SigningVariables"/>
    /// are set: KEYSTORE_PATH (an existing keystore file), KEYSTORE_PASS, KEY_ALIAS and KEY_PASS.
    /// Otherwise Unity signs it with the debug key. A build started with -release fails unless all
    /// four are set.</item>
    /// <item>versionCode is VERSION_CODE when set, otherwise MAJOR*1000000 + MINOR*1000 + PATCH of
    /// the version name (VERSION_NAME when set, otherwise Player Settings' bundleVersion), so 0.1.0
    /// is 1000 and 1.2.3 is 1002003.</item>
    /// </list>
    /// The build methods apply these settings for one build and put the previous Player Settings
    /// back afterwards, so ProjectSettings.asset never records a keystore path or alias.
    /// </remarks>
    public static class AndroidReleaseSettings
    {
        public const string KeystorePathVariable = "KEYSTORE_PATH";
        public const string KeystorePassVariable = "KEYSTORE_PASS";
        public const string KeyAliasVariable = "KEY_ALIAS";
        public const string KeyPassVariable = "KEY_PASS";
        public const string VersionCodeVariable = "VERSION_CODE";
        public const string VersionNameVariable = "VERSION_NAME";
        public const string ReleaseArgument = "-release";

        /// <summary>Environment variables that must all be set to sign with a release key.</summary>
        public static readonly IReadOnlyList<string> SigningVariables = new[]
        {
            KeystorePathVariable, KeystorePassVariable, KeyAliasVariable, KeyPassVariable
        };

        /// <summary>Highest versionCode Google Play and Meta accept.</summary>
        public const int MaxVersionCode = 2100000000;

        public sealed class Signing
        {
            internal string KeystorePath;
            internal string KeystorePass;
            internal string KeyAlias;
            internal string KeyPass;

            /// <summary>Names (never values) of the signing variables that are unset or empty.</summary>
            public IReadOnlyList<string> Missing { get; internal set; } = Array.Empty<string>();

            public bool IsComplete => Missing.Count == 0;

            /// <summary>True when some but not all signing variables are set (probably a mistake).</summary>
            public bool IsPartial => Missing.Count > 0 && Missing.Count < SigningVariables.Count;
        }

        /// <summary>Reads the signing variables through <paramref name="getVariable"/> (normally the environment).</summary>
        public static Signing ReadSigning(Func<string, string> getVariable)
        {
            string Get(string name) => getVariable(name) is { } value && value.Trim().Length > 0 ? value : null;

            var signing = new Signing
            {
                KeystorePath = Get(KeystorePathVariable),
                KeystorePass = Get(KeystorePassVariable),
                KeyAlias = Get(KeyAliasVariable),
                KeyPass = Get(KeyPassVariable)
            };
            signing.Missing = SigningVariables.Where(name => Get(name) == null).ToList();
            return signing;
        }

        /// <summary>
        /// Throws <see cref="BuildFailedException"/> when a -release build lacks a complete signing
        /// configuration, or when the keystore file does not exist. Names variables, never values.
        /// </summary>
        public static void Validate(Signing signing, bool release)
        {
            if (release && !signing.IsComplete)
            {
                throw new BuildFailedException(
                    $"{ReleaseArgument} needs {string.Join(", ", SigningVariables)} in the environment to sign with the " +
                    $"release key. Missing: {string.Join(", ", signing.Missing)}.");
            }

            if (signing.IsComplete && !File.Exists(signing.KeystorePath))
            {
                throw new BuildFailedException($"{KeystorePathVariable} does not point to an existing keystore file.");
            }
        }

        /// <summary>Version name for the build: VERSION_NAME when set, otherwise <paramref name="bundleVersion"/>.</summary>
        public static string ResolveVersionName(Func<string, string> getVariable, string bundleVersion)
        {
            var fromEnvironment = getVariable(VersionNameVariable);
            return string.IsNullOrWhiteSpace(fromEnvironment) ? bundleVersion?.Trim() : fromEnvironment.Trim();
        }

        /// <summary>VERSION_CODE when set, otherwise <see cref="ComputeVersionCode"/> of <paramref name="versionName"/>.</summary>
        public static int ResolveVersionCode(Func<string, string> getVariable, string versionName)
        {
            var fromEnvironment = getVariable(VersionCodeVariable);
            if (string.IsNullOrWhiteSpace(fromEnvironment))
            {
                return ComputeVersionCode(versionName);
            }

            if (!int.TryParse(fromEnvironment.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var code) ||
                code < 1 || code > MaxVersionCode)
            {
                throw new BuildFailedException(
                    $"{VersionCodeVariable} must be a whole number from 1 to {MaxVersionCode}, got \"{fromEnvironment.Trim()}\".");
            }

            return code;
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
                    $"Cannot derive versionCode from version \"{versionName}\": use MAJOR.MINOR.PATCH with MINOR and PATCH " +
                    $"at most 999 (versionCode = MAJOR*1000000 + MINOR*1000 + PATCH), or set {VersionCodeVariable}.");
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
