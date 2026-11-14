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
    /// agreement (<see cref="CaptureConsentRequiredException"/>). The answer is kept on the headset
    /// only, in PlayerPrefs: the notice version and the UTC time of the agreement. Settings can
    /// withdraw it, and uninstalling the app deletes it.
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
        /// Raised when a capture needs the agreement first. The arguments are the vision server
        /// URL the snapshot would go to and the action that continues the capture once the user
        /// agrees.
        /// </summary>
        public static event Action<string, Action> Requested;

        /// <summary>True once the user has agreed to the current <see cref="NoticeVersion"/>.</summary>
        public static bool IsGranted => PlayerPrefs.GetInt(PlayerPrefsVersionKey, 0) >= NoticeVersion;

        /// <summary>When the user agreed (UTC, ISO 8601), or empty while <see cref="IsGranted"/> is false.</summary>
        public static string AgreedAtUtc => IsGranted ? PlayerPrefs.GetString(PlayerPrefsAgreedAtKey, string.Empty) : string.Empty;

        /// <summary>Records the agreement to the current notice, now.</summary>
        public static void Grant()
        {
            Grant(DateTime.UtcNow);
        }

        /// <summary>Records the agreement to the current notice at <paramref name="agreedAt"/>.</summary>
        public static void Grant(DateTime agreedAt)
        {
            PlayerPrefs.SetInt(PlayerPrefsVersionKey, NoticeVersion);
            PlayerPrefs.SetString(PlayerPrefsAgreedAtKey, FormatUtc(agreedAt));
            PlayerPrefs.Save();
        }

        /// <summary>Deletes the record, so the next capture asks again.</summary>
        public static void Withdraw()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsVersionKey);
            PlayerPrefs.DeleteKey(PlayerPrefsAgreedAtKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// True when snapshots may be sent. Otherwise raises <see cref="Requested"/> with
        /// <paramref name="serverUrl"/> and <paramref name="continueCapture"/> and returns false.
        /// With nothing listening, nothing can be agreed to, so nothing is sent.
        /// </summary>
        public static bool EnsureGranted(string serverUrl, Action continueCapture)
        {
            if (IsGranted)
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
