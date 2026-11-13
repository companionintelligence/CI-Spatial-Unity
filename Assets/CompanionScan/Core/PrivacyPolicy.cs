using UnityEngine;

namespace CompanionScan.Core
{
    /// <summary>
    /// The app's published privacy policy. The settings panel and the camera notice show and open
    /// it, because the Meta Horizon Store and Google Play both want the policy reachable inside the
    /// app, not only from the listing.
    /// </summary>
    public static class PrivacyPolicy
    {
        /// <summary>
        /// The policy page on the company website, the same URL as the "Privacy Policy" field of
        /// the store listings. Change it here and in the listings together.
        /// </summary>
        public const string Url = "https://ci.computer/privacy/companion-scan";

        /// <summary><see cref="Url"/> without the scheme, short enough for a panel line.</summary>
        public static string DisplayUrl => Url.StartsWith("https://") ? Url.Substring("https://".Length) : Url;

        /// <summary>Opens the policy in the headset's browser.</summary>
        public static void Open()
        {
            Debug.Log("[Privacy] Opening the privacy policy: " + Url);
            Application.OpenURL(Url);
        }
    }
}
