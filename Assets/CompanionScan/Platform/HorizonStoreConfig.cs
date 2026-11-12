using UnityEngine;

namespace CompanionScan.Platform
{
    /// <summary>
    /// Meta Horizon Store settings, loaded at runtime from Resources/HorizonStoreConfig.
    /// The repository ships it without an app ID: the app owner enters the ID from the
    /// Meta Horizon developer dashboard (API page) before making a store build.
    /// Used by <see cref="HorizonStoreEntitlementCheck"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "HorizonStoreConfig", menuName = "CompanionScan/Horizon Store Config")]
    public sealed class HorizonStoreConfig : ScriptableObject
    {
        /// <summary>Path of the asset under a Resources folder.</summary>
        public const string ResourcesPath = "HorizonStoreConfig";

        [Tooltip("Meta Horizon Store app ID (developer dashboard > API). Supplied by the app owner.")]
        public string appId = string.Empty;

        [Tooltip("Quit when the entitlement check fails, as the store requires.")]
        public bool quitWhenNotEntitled = true;

        public string TrimmedAppId => (appId ?? string.Empty).Trim();

        public static HorizonStoreConfig Load() => Resources.Load<HorizonStoreConfig>(ResourcesPath);
    }
}
