using System;
using UnityEngine;

namespace CompanionScan.Core
{
    [CreateAssetMenu(fileName = "ServerConfig", menuName = "CompanionScan/Server Config")]
    public class ServerConfig : ScriptableObject
    {
        public const string PlayerPrefsBaseUrlKey = "CompanionScan.ServerBaseUrl";
        public string baseUrl = "https://liam-razer.capybara-ulmer.ts.net";
        public float boxThreshold = 0.35f;
        public float textThreshold = 0.25f;

        public string ResolveBaseUrl()
        {
            var overrideUrl = PlayerPrefs.GetString(PlayerPrefsBaseUrlKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(overrideUrl))
            {
                return overrideUrl;
            }

            var envUrl = Environment.GetEnvironmentVariable("CompanionScan_SERVER_URL");
            if (!string.IsNullOrWhiteSpace(envUrl))
            {
                return envUrl;
            }

            return baseUrl;
        }
    }
}

