using System.IO;
using NUnit.Framework;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// "Enable only in debug builds" keeps Meta's Immersive Debugger panel out of release builds,
    /// but in Meta XR Core 209 its log cache (ConsoleLogsCache) still subscribes to every log
    /// message whenever the debugger is enabled, and with no debugger console to drain it, it keeps
    /// every message for the life of a release build. So the committed setting is off; turn it on
    /// locally (Meta > Immersive Debugger) for a development build and do not commit that.
    /// </summary>
    public class ImmersiveDebuggerSettingsTests
    {
        private const string SettingsPath = "Assets/Resources/ImmersiveDebuggerSettings.asset";

        [Test]
        public void ImmersiveDebugger_IsOffInTheCommittedSettings()
        {
            StringAssert.IsMatch(@"(?m)^  immersiveDebuggerEnabled: 0\r?$", File.ReadAllText(SettingsPath));
        }

        [Test]
        public void ImmersiveDebugger_StaysDebugBuildOnlyWhenTurnedOnLocally()
        {
            StringAssert.IsMatch(@"(?m)^  enableOnlyInDebugBuild: 1\r?$", File.ReadAllText(SettingsPath));
        }
    }
}
