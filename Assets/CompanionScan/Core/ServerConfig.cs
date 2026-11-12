using System;
using System.Collections.Generic;
using UnityEngine;

namespace CompanionScan.Core
{
    /// <summary>
    /// CV server connection settings.
    ///
    /// The shipped asset carries no server URL. Users enter it in-headset
    /// (see <c>CompanionScan.UI.SettingsPanel</c>); it is stored as a PlayerPrefs
    /// override that takes precedence over the serialized defaults. Runtime code
    /// never writes to this asset, so values entered in Play Mode cannot leak into
    /// version control.
    /// </summary>
    [CreateAssetMenu(fileName = "ServerConfig", menuName = "CompanionScan/Server Config")]
    public class ServerConfig : ScriptableObject
    {
        /// <summary>Path of the default asset under a Resources folder.</summary>
        public const string ResourcesPath = "ServerConfig";

        public const string PlayerPrefsBaseUrlKey = "CompanionScan.ServerBaseUrl";
        public const string PlayerPrefsBoxThresholdKey = "CompanionScan.BoxThreshold";
        public const string PlayerPrefsTextThresholdKey = "CompanionScan.TextThreshold";

        /// <summary>
        /// Editor-only URL override. Environment variables cannot be set for an
        /// installed APK, so players ignore it.
        /// </summary>
        public const string EditorServerUrlEnvVar = "CompanionScan_SERVER_URL";

        /// <summary>Example shown as a placeholder; not a real server.</summary>
        public const string ExampleUrl = "https://cv.example.com";

        [Tooltip("Optional built-in default URL. Keep empty in the repository: users set the URL in-headset (left Menu button).")]
        public string baseUrl = string.Empty;
        public float boxThreshold = 0.35f;
        public float textThreshold = 0.25f;
        public int requestTimeoutSeconds = 6;
        public int retryCount = 2;
        public float retryDelaySeconds = 0.5f;

        [Tooltip("LAN hosts (IP address or LAN host name, without scheme or port) that DEVELOPMENT builds may reach over plain http. " +
                 "Release builds are HTTPS-only. Applied to the Android network security config at build time.")]
        public List<string> developmentCleartextHosts = new List<string>();

        /// <summary>
        /// Effective server URL: PlayerPrefs override, then (Editor only) the
        /// <see cref="EditorServerUrlEnvVar"/> environment variable, then <see cref="baseUrl"/>.
        /// Returns an empty string when nothing is configured.
        /// </summary>
        public string ResolveBaseUrl()
        {
            var overrideUrl = PlayerPrefs.GetString(PlayerPrefsBaseUrlKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(overrideUrl))
            {
                return overrideUrl.Trim();
            }

#if UNITY_EDITOR
            var envUrl = Environment.GetEnvironmentVariable(EditorServerUrlEnvVar);
            if (!string.IsNullOrWhiteSpace(envUrl))
            {
                return envUrl.Trim();
            }
#endif

            return string.IsNullOrWhiteSpace(baseUrl) ? string.Empty : baseUrl.Trim();
        }

        /// <summary>True when a server URL is configured from any source.</summary>
        public bool HasBaseUrl => !string.IsNullOrWhiteSpace(ResolveBaseUrl());

        public float ResolveBoxThreshold() => PlayerPrefs.GetFloat(PlayerPrefsBoxThresholdKey, boxThreshold);

        public float ResolveTextThreshold() => PlayerPrefs.GetFloat(PlayerPrefsTextThresholdKey, textThreshold);

        /// <summary>Persist a server URL override (already normalized and validated).</summary>
        public void SaveBaseUrlOverride(string url)
        {
            PlayerPrefs.SetString(PlayerPrefsBaseUrlKey, url ?? string.Empty);
            PlayerPrefs.Save();
        }

        /// <summary>Remove the URL override so the serialized default applies again.</summary>
        public void ClearBaseUrlOverride()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsBaseUrlKey);
            PlayerPrefs.Save();
        }

        public void SaveThresholdOverrides(float? box, float? text)
        {
            if (box.HasValue)
            {
                PlayerPrefs.SetFloat(PlayerPrefsBoxThresholdKey, box.Value);
            }

            if (text.HasValue)
            {
                PlayerPrefs.SetFloat(PlayerPrefsTextThresholdKey, text.Value);
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Checks <paramref name="url"/> against this build's transport policy:
        /// https always; plain http only in development builds and, on Android,
        /// only to hosts in <see cref="developmentCleartextHosts"/> (the same list
        /// the build writes into the network security config).
        /// </summary>
        public bool IsUrlAllowed(string url, out string error)
        {
            return CheckUrlPolicy(
                url,
                Debug.isDebugBuild,
                Application.platform == RuntimePlatform.Android,
                developmentCleartextHosts,
                out error);
        }

        /// <summary>
        /// Normalizes user input into a base URL: trims, defaults to https:// when no
        /// scheme is given, and drops a trailing slash. Rejects credentials, query
        /// strings and fragments.
        /// </summary>
        public static bool TryNormalizeBaseUrl(string input, out string normalized, out string error)
        {
            normalized = string.Empty;
            error = null;

            var text = (input ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                error = $"Enter the CV server URL, for example {ExampleUrl}";
                return false;
            }

            if (text.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                text = "https://" + text;
            }

            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
            {
                error = $"\"{input.Trim()}\" is not a valid URL.";
                return false;
            }

            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            {
                error = $"Unsupported scheme \"{uri.Scheme}\". Use https://.";
                return false;
            }

            if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            {
                error = "Enter only the scheme, host, optional port and path.";
                return false;
            }

            normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            return true;
        }

        /// <summary>Pure form of <see cref="IsUrlAllowed"/> for tests and tools.</summary>
        public static bool CheckUrlPolicy(
            string url,
            bool developmentBuild,
            bool enforceCleartextAllowList,
            IEnumerable<string> cleartextHosts,
            out string error)
        {
            error = null;
            if (!Uri.TryCreate((url ?? string.Empty).Trim(), UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
            {
                error = "CV server URL is not a valid absolute URL.";
                return false;
            }

            if (uri.Scheme == Uri.UriSchemeHttps)
            {
                return true;
            }

            if (uri.Scheme != Uri.UriSchemeHttp)
            {
                error = $"Unsupported scheme \"{uri.Scheme}\". Use https://.";
                return false;
            }

            if (!developmentBuild)
            {
                error = "Plain http:// is blocked in release builds. Use https://.";
                return false;
            }

            if (enforceCleartextAllowList && !CleartextHostPolicy.Contains(cleartextHosts, uri.Host))
            {
                error = $"Plain http:// to {uri.Host} is not allowed in this build. Use https://, or add the host to " +
                        "ServerConfig > Development Cleartext Hosts and make a new development build.";
                return false;
            }

            return true;
        }
    }
}
