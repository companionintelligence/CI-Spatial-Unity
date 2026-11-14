using System;
using System.Globalization;
using UnityEngine;

namespace CompanionScan.Core
{
    /// <summary>
    /// The user's agreement to send camera snapshots off the headset, and the record of it.
    ///
    /// Passthrough camera images are Device User Data under Meta's Developer Data Use Policy, so
    /// no snapshot leaves the headset before the user has agreed: the CV pipeline
    /// (<c>SpatialCapturePipeline</c>) calls <see cref="EnsureGranted"/> before it takes a frame,
    /// and the camera notice (<c>CompanionScan.UI.CaptureConsentPanel</c>) asks the user. Every
    /// image request then passes <see cref="CVServerClient"/>, which refuses to send without the
    /// agreement (<see cref="CaptureConsentRequiredException"/>).
    ///
    /// The notice names the vision server, so the agreement covers that server only: after the
    /// server URL changes, the next capture asks again. The answer is kept on the headset only, in
    /// PlayerPrefs: the notice version, the UTC time of the agreement and the server URL. Settings
    /// can withdraw it, and uninstalling the app deletes it.
    /// </summary>
    public static class CaptureConsent
    {
        /// <summary>
        /// Version of the notice text. Raise it when the notice changes what it says is sent or
        /// where, so that every user is asked again.
        /// </summary>
        public const int NoticeVersion = 1;

        /// <summary>PlayerPrefs key of the notice version the user agreed to.</summary>
        public const string PlayerPrefsVersionKey = "CompanionScan.CaptureConsent.Version";

        /// <summary>PlayerPrefs key of the UTC time of the agreement (ISO 8601).</summary>
        public const string PlayerPrefsAgreedAtKey = "CompanionScan.CaptureConsent.AgreedAtUtc";

        /// <summary>
        /// PlayerPrefs key of the vision server URL the agreement covers: the one the notice named,
        /// in the form of <see cref="NormalizeServerUrl"/>.
        /// </summary>
        public const string PlayerPrefsServerUrlKey = "CompanionScan.CaptureConsent.ServerUrl";

        /// <summary>
        /// Raised when a capture needs the agreement first. The arguments are the vision server
        /// URL the snapshot would go to and the action that continues the capture once the user
        /// agrees.
        /// </summary>
        public static event Action<string, Action> Requested;

        /// <summary>
        /// True once the user has agreed to the current <see cref="NoticeVersion"/> for a server
        /// (<see cref="AgreedServerUrl"/>). Whether that covers a capture is <see cref="IsGrantedFor"/>.
        /// </summary>
        public static bool IsGranted =>
            PlayerPrefs.GetInt(PlayerPrefsVersionKey, 0) >= NoticeVersion &&
            PlayerPrefs.GetString(PlayerPrefsServerUrlKey, string.Empty).Length > 0;

        /// <summary>When the user agreed (UTC, ISO 8601), or empty while <see cref="IsGranted"/> is false.</summary>
        public static string AgreedAtUtc => IsGranted ? PlayerPrefs.GetString(PlayerPrefsAgreedAtKey, string.Empty) : string.Empty;

        /// <summary>The server the user agreed to send snapshots to, or empty while <see cref="IsGranted"/> is false.</summary>
        public static string AgreedServerUrl => IsGranted ? PlayerPrefs.GetString(PlayerPrefsServerUrlKey, string.Empty) : string.Empty;

        /// <summary>True when the user has agreed to send snapshots to <paramref name="serverUrl"/>.</summary>
        public static bool IsGrantedFor(string serverUrl)
        {
            var url = NormalizeServerUrl(serverUrl);
            return url.Length > 0 && string.Equals(AgreedServerUrl, url, StringComparison.Ordinal);
        }

        /// <summary>Records the agreement to the current notice for <paramref name="serverUrl"/>, now.</summary>
        public static void Grant(string serverUrl)
        {
            Grant(serverUrl, DateTime.UtcNow);
        }

        /// <summary>
        /// Records the agreement to the current notice for <paramref name="serverUrl"/> at
        /// <paramref name="agreedAt"/>. It replaces an agreement for another server. Without a
        /// server nothing is recorded.
        /// </summary>
        public static void Grant(string serverUrl, DateTime agreedAt)
        {
            var url = NormalizeServerUrl(serverUrl);
            if (url.Length == 0)
            {
                Debug.LogWarning("[Consent] The notice named no vision server, so no agreement is recorded.");
                return;
            }

            PlayerPrefs.SetInt(PlayerPrefsVersionKey, NoticeVersion);
            PlayerPrefs.SetString(PlayerPrefsAgreedAtKey, FormatUtc(agreedAt));
            PlayerPrefs.SetString(PlayerPrefsServerUrlKey, url);
            PlayerPrefs.Save();
        }

        /// <summary>Deletes the record, so the next capture asks again.</summary>
        public static void Withdraw()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsVersionKey);
            PlayerPrefs.DeleteKey(PlayerPrefsAgreedAtKey);
            PlayerPrefs.DeleteKey(PlayerPrefsServerUrlKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// True when snapshots may be sent to <paramref name="serverUrl"/>. Otherwise raises
        /// <see cref="Requested"/> with <paramref name="serverUrl"/> and
        /// <paramref name="continueCapture"/> and returns false. With nothing listening, nothing can
        /// be agreed to, so nothing is sent.
        /// </summary>
        public static bool EnsureGranted(string serverUrl, Action continueCapture)
        {
            if (IsGrantedFor(serverUrl))
            {
                return true;
            }

            var handler = Requested;
            if (handler == null)
            {
                Debug.LogWarning("[Consent] No camera notice is available to ask for agreement, so no snapshot is sent.");
                return false;
            }

            handler(serverUrl ?? string.Empty, continueCapture);
            return false;
        }

        /// <summary>The ISO 8601 form stored in <see cref="PlayerPrefsAgreedAtKey"/>.</summary>
        public static string FormatUtc(DateTime time)
        {
            return time.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The form a server URL is recorded and compared in: that of
        /// <see cref="ServerConfig.TryNormalizeBaseUrl"/> (lower-case scheme and host, no default
        /// port or trailing slash), or the trimmed text when it is not a valid server URL.
        /// </summary>
        public static string NormalizeServerUrl(string serverUrl)
        {
            var text = (serverUrl ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return string.Empty;
            }

            return ServerConfig.TryNormalizeBaseUrl(text, out var normalized, out _) ? normalized : text.TrimEnd('/');
        }
    }

    /// <summary>
    /// Thrown by <see cref="CVServerClient"/> instead of sending an image the user has not agreed to
    /// send (<see cref="CaptureConsent"/>). Nothing was sent.
    /// </summary>
    public sealed class CaptureConsentRequiredException : Exception
    {
        public CaptureConsentRequiredException(string serverUrl)
            : base("Nothing was sent: the user has not agreed to send camera snapshots to " +
                   (string.IsNullOrWhiteSpace(serverUrl) ? "the vision server" : serverUrl.Trim()) + ".")
        {
            ServerUrl = serverUrl ?? string.Empty;
        }

        /// <summary>The server the image would have gone to.</summary>
        public string ServerUrl { get; }
    }
}
