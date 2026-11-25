using System;
using UnityEngine;
#if COMPANIONSCAN_HORIZON_STORE
using PlatformCore = Oculus.Platform.Core;
using PlatformEntitlements = Oculus.Platform.Entitlements;
using PlatformMessage = Oculus.Platform.Message;
using PlatformInitializeMessage = Oculus.Platform.Message<Oculus.Platform.Models.PlatformInitialize>;
#endif

namespace CompanionScan.Platform
{
    /// <summary>
    /// Meta Horizon Store entitlement check (scaffold).
    ///
    /// Compiled in only when the scripting define <see cref="ScriptingDefine"/> is set
    /// (for example in the Meta Quest build profile used for store builds), so
    /// development and sideloaded builds are unaffected. When enabled, it starts after
    /// the first scene loads, initializes the Meta Platform SDK with the app ID from
    /// <see cref="HorizonStoreConfig"/> and asks whether the user is entitled to the
    /// app. If the check fails (no app ID, initialization error or no entitlement) the
    /// app quits, as the store requires, unless
    /// <see cref="HorizonStoreConfig.quitWhenNotEntitled"/> is off.
    /// </summary>
    public sealed class HorizonStoreEntitlementCheck : MonoBehaviour
    {
        public const string ScriptingDefine = "COMPANIONSCAN_HORIZON_STORE";

        public enum Result
        {
            NotChecked,
            Pending,
            Entitled,
            NotEntitled,
            Failed
        }

        /// <summary>Outcome of the check in this session.</summary>
        public static Result Status { get; private set; } = Result.NotChecked;

        /// <summary>Raised once when the check completes.</summary>
        public static event Action<Result> Completed;

        private static void Publish(Result result)
        {
            Status = result;
            Completed?.Invoke(result);
        }

#if COMPANIONSCAN_HORIZON_STORE
        private HorizonStoreConfig _config;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<HorizonStoreEntitlementCheck>() != null)
            {
                return;
            }

            var host = new GameObject(nameof(HorizonStoreEntitlementCheck));
            DontDestroyOnLoad(host);
            host.AddComponent<HorizonStoreEntitlementCheck>();
        }

        private void Start()
        {
            if (Status != Result.NotChecked)
            {
                return;
            }

            _config = HorizonStoreConfig.Load();
            var appId = _config != null ? _config.TrimmedAppId : string.Empty;
            if (appId.Length == 0)
            {
                Finish(Result.Failed, "No Meta Horizon Store app ID configured (Resources/" + HorizonStoreConfig.ResourcesPath + ").");
                return;
            }

            Status = Result.Pending;
            try
            {
                PlatformCore.AsyncInitialize(appId).OnComplete(OnPlatformInitialized);
            }
            catch (Exception e)
            {
                Finish(Result.Failed, "Meta Platform SDK failed to initialize: " + e.Message);
            }
        }

        private void OnPlatformInitialized(PlatformInitializeMessage message)
        {
            if (message.IsError)
            {
                Finish(Result.Failed, "Meta Platform SDK failed to initialize: " + message.GetError().Message);
                return;
            }

            PlatformEntitlements.IsUserEntitledToApplication().OnComplete(OnEntitlementChecked);
        }

        private void OnEntitlementChecked(PlatformMessage message)
        {
            if (message.IsError)
            {
                Finish(Result.NotEntitled, "The user is not entitled to this app: " + message.GetError().Message);
            }
            else
            {
                Finish(Result.Entitled, null);
            }
        }

        private void Finish(Result result, string error)
        {
            if (result == Result.Entitled)
            {
                Debug.Log("[Entitlement] Meta Horizon Store entitlement check passed.");
            }
            else
            {
                Debug.LogError("[Entitlement] " + error);
            }

            Publish(result);

            if (result != Result.Entitled && (_config == null || _config.quitWhenNotEntitled))
            {
                Application.Quit();
            }
        }
#endif
    }
}
