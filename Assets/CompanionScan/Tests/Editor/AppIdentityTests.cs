using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// The headset shows the product name (the APK label, also used in Android's permission
    /// prompts) and the store lists the app as Companion Scan. The package name is permanent from
    /// the first store upload, so renaming the product must not change it: it is set explicitly
    /// instead of being derived from the company and product names.
    /// </summary>
    public class AppIdentityTests
    {
        private const string ProductName = "Companion Scan";
        private const string PackageName = "com.CompanionIntelligence.CompanionCapture";
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";
        private const string BuildProfilePath = "Assets/Settings/Build Profiles/Meta Quest.asset";

        [Test]
        public void PlayerSettings_UseTheProductName()
        {
            Assert.AreEqual(ProductName, PlayerSettings.productName);
            StringAssert.IsMatch(@"(?m)^  productName: Companion Scan\r?$", File.ReadAllText(ProjectSettingsPath));
        }

        [Test]
        public void MetaQuestBuildProfile_UsesTheProductName()
        {
            StringAssert.Contains("'|   productName: Companion Scan'", File.ReadAllText(BuildProfilePath));
        }

        [Test]
        public void PackageName_IsUnchangedByTheRename()
        {
            Assert.AreEqual(PackageName, PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
        }

        [TestCase(ProjectSettingsPath, @"(?m)^  overrideDefaultApplicationIdentifier: 1\r?$", @"(?m)^    Android: com\.CompanionIntelligence\.CompanionCapture\r?$")]
        [TestCase(BuildProfilePath, @"'\|   overrideDefaultApplicationIdentifier: 1'", @"'\|     Android: com\.CompanionIntelligence\.CompanionCapture'")]
        public void PackageName_IsSetExplicitly(string path, string overridePattern, string packagePattern)
        {
            var text = File.ReadAllText(path);

            Assert.IsTrue(Regex.IsMatch(text, overridePattern), path + " derives the package name from the product name");
            Assert.IsTrue(Regex.IsMatch(text, packagePattern), path + " does not set the Android package name");
        }

        /// <summary>
        /// The old label, spelled CompanionCapture or Companion Capture. The package name
        /// (com.CompanionIntelligence.CompanionCapture) and the main scene's file name
        /// (CompanionCapture.unity) are not labels and stay.
        /// </summary>
        private static readonly Regex OldLabel = new Regex(@"(?<![\w./\\])Companion ?Capture(?!\.unity)", RegexOptions.IgnoreCase);

        private static readonly Regex StringLiteral = new Regex(@"""(?:[^""\\\r\n]|\\.)*""");

        [Test]
        public void NoUserFacingTextUsesTheOldLabel()
        {
            foreach (var file in Directory.GetFiles("Assets/CompanionScan", "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Tests/"))
                {
                    continue;
                }

                foreach (Match literal in StringLiteral.Matches(File.ReadAllText(file)))
                {
                    Assert.IsFalse(OldLabel.IsMatch(literal.Value), $"{file} shows the old label: {literal.Value}");
                }
            }

            // Scenes and prefabs: text components, object names and any other serialized text.
            var assets = Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets", "*.prefab", SearchOption.AllDirectories))
                .ToList();
            Assert.That(assets, Has.Some.EndsWith("CompanionCapture.unity"), "the main scene was not checked");
            foreach (var file in assets)
            {
                var match = OldLabel.Match(File.ReadAllText(file));
                Assert.IsFalse(match.Success, $"{file} contains the old label \"{match.Value}\"");
            }
        }

        [TestCase("productName: Companion Capture", true)]
        [TestCase("m_text: \"Welcome to CompanionCapture\"", true)]
        [TestCase("\"companioncapture\"", true)]
        [TestCase("Android: com.CompanionIntelligence.CompanionCapture", false)]
        [TestCase("path: Assets/CompanionScan/Scenes/CompanionCapture.unity", false)]
        [TestCase("\"Companion Scan\"", false)]
        public void OldLabelPattern_FindsBothSpellingsButNotThePackageOrSceneName(string text, bool found)
        {
            Assert.AreEqual(found, OldLabel.IsMatch(text));
        }
    }
}
