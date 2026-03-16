using System;
using NUnit.Framework;

namespace CompanionScan.Tests.PlayMode
{
    internal static class TestRuntimeFlags
    {
        private const string DeterministicOnlyEnv = "CompanionScan_TEST_DETERMINISTIC_ONLY";
        private const string EnableLiveIntegrationEnv = "CompanionScan_TEST_ENABLE_LIVE_INTEGRATION";
        private const string AutoUpdateSnapshotsEnv = "CompanionScan_TEST_AUTO_UPDATE_SNAPSHOTS";

        public static bool IsDeterministicOnly => ReadBool(DeterministicOnlyEnv, false);
        public static bool IsLiveIntegrationEnabled => ReadBool(EnableLiveIntegrationEnv, false);
        public static bool IsSnapshotAutoUpdateEnabled => ReadBool(AutoUpdateSnapshotsEnv, false);

        public static void RequireLiveIntegrationOrIgnore(string reason)
        {
            if (!IsLiveIntegrationEnabled || IsDeterministicOnly)
            {
                Assert.Ignore(reason + $" ({EnableLiveIntegrationEnv}=true required)");
            }
        }

        private static bool ReadBool(string env, bool defaultValue)
        {
            var raw = Environment.GetEnvironmentVariable(env);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            switch (raw.Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "yes":
                case "on":
                    return true;
                case "0":
                case "false":
                case "no":
                case "off":
                    return false;
                default:
                    return defaultValue;
            }
        }
    }
}