using System.IO;
using CompanionScan.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace CompanionScan.Tests.Editor
{
    public class QuestTargetSdkTests
    {
        private const string BuildProfilePath = "Assets/Settings/Build Profiles/Meta Quest.asset";

        [Test]
        public void PlayerSettings_TargetApiLevel34()
        {
            Assert.AreEqual(QuestTargetSdkRequirement.RequiredTargetSdk, (int)PlayerSettings.Android.targetSdkVersion);
            Assert.DoesNotThrow(() => QuestTargetSdkRequirement.Verify(PlayerSettings.Android.targetSdkVersion));
        }

        [Test]
        public void MetaQuestBuildProfile_TargetsApiLevel34()
        {
            StringAssert.Contains("'|   AndroidTargetSdkVersion: 34'", File.ReadAllText(BuildProfilePath));
        }

        [TestCase(AndroidSdkVersions.AndroidApiLevelAuto)]
        [TestCase((AndroidSdkVersions)32)]
        [TestCase((AndroidSdkVersions)35)]
        [TestCase((AndroidSdkVersions)36)]
        public void OtherTargetApiLevels_FailTheBuild(AndroidSdkVersions targetSdk)
        {
            Assert.Throws<BuildFailedException>(() => QuestTargetSdkRequirement.Verify(targetSdk));
        }
    }
}
