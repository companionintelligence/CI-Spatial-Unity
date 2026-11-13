using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using NUnit.Framework;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// Companion Scan needs the Passthrough Camera API, which only Quest 3 and Quest 3S have,
    /// so every place that declares supported headsets must list exactly those two.
    /// </summary>
    public class QuestDeviceTargetTests
    {
        private const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string OpenXRSettingsPath = "Assets/XR/Settings/OpenXR Package Settings.asset";
        private const string OvrProjectConfigPath = "Assets/Oculus/OculusProjectConfig.asset";
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

        [Test]
        public void Manifest_SupportsOnlyQuest3And3S()
        {
            var manifest = new XmlDocument();
            manifest.Load(ManifestPath);

            string value = null;
            foreach (XmlElement element in manifest.SelectNodes("/manifest/application/meta-data"))
            {
                if (element.GetAttribute("name", AndroidNamespace) == "com.oculus.supportedDevices")
                {
                    value = element.GetAttribute("value", AndroidNamespace);
                }
            }

            Assert.AreEqual("quest3|quest3s", value);
        }

        [Test]
        public void OpenXRMetaQuestFeature_TargetsOnlyQuest3And3S()
        {
            // OpenXR uses the codename "eureka" for Quest 3.
            CollectionAssert.AreEquivalent(new[] { "eureka", "quest3s" }, EnabledOpenXRTargets("MetaQuestFeature Android"));
        }

        [Test]
        public void OvrProjectConfig_TargetsOnlyQuest3And3S()
        {
            // OVRProjectConfig.DeviceType: Quest3 = 4, Quest3S = 5.
            var match = Regex.Match(File.ReadAllText(OvrProjectConfigPath), @"^\s*targetDeviceTypes: ([0-9a-f]+)i?\s*$",
                RegexOptions.Multiline);
            Assert.IsTrue(match.Success, "targetDeviceTypes not found");

            var hex = match.Groups[1].Value;
            var types = new List<int>();
            for (var i = 0; i + 8 <= hex.Length; i += 8)
            {
                types.Add(BitConverter.ToInt32(Enumerable.Range(0, 4)
                    .Select(b => Convert.ToByte(hex.Substring(i + b * 2, 2), 16)).ToArray(), 0));
            }

            CollectionAssert.AreEquivalent(new[] { 4, 5 }, types);
        }

        private static List<string> EnabledOpenXRTargets(string featureName)
        {
            var enabled = new List<string>();
            var inFeature = false;
            string manifestName = null;
            foreach (var line in File.ReadAllLines(OpenXRSettingsPath))
            {
                if (line.StartsWith("  m_Name: ", StringComparison.Ordinal))
                {
                    inFeature = line.Trim() == "m_Name: " + featureName;
                    continue;
                }

                if (!inFeature)
                {
                    continue;
                }

                var name = Regex.Match(line, @"^    manifestName: (\S+)$");
                if (name.Success)
                {
                    manifestName = name.Groups[1].Value;
                    continue;
                }

                var state = Regex.Match(line, @"^    enabled: ([01])$");
                if (state.Success && manifestName != null)
                {
                    if (state.Groups[1].Value == "1")
                    {
                        enabled.Add(manifestName);
                    }

                    manifestName = null;
                }
            }

            Assert.IsNotEmpty(enabled, featureName + " has no enabled target devices");
            return enabled;
        }
    }
}
