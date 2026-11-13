using System.IO;
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

        [Test]
        public void NoUserFacingTextUsesTheOldLabel()
        {
            foreach (var file in Directory.GetFiles("Assets/CompanionScan", "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Tests/"))
                {
                    continue;
                }

                StringAssert.DoesNotContain("\"CompanionCapture", File.ReadAllText(file), file);
            }
        }
    }
}
