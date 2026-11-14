using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CompanionScan.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace CompanionScan.Tests.Editor
{
    public class AndroidReleaseSettingsTests
    {
        private const string StorePass = "test-store-pass";
        private const string KeyPass = "test-key-pass";

        private string _tempRoot;

        [SetUp]
        public void CreateTempRoot()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "release-settings-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
        }

        [TearDown]
        public void DeleteTempRoot()
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }

        private static Func<string, string> Env(Dictionary<string, string> values) =>
            name => values.TryGetValue(name, out var value) ? value : null;

        private static Dictionary<string, string> AllSigningVariables(string keystorePath) => new()
        {
            { "ANDROID_KEYSTORE_NAME", keystorePath },
            { "ANDROID_KEYSTORE_PASS", StorePass },
            { "ANDROID_KEYALIAS_NAME", "test-alias" },
            { "ANDROID_KEYALIAS_PASS", KeyPass }
        };

        [Test]
        public void SigningVariables_AreTheGameCINames()
        {
            // CI passes these names into GameCI's build container, and the release setup script and
            // the other Quest app use the same ones: renaming one breaks signing silently.
            CollectionAssert.AreEqual(
                new[] { "ANDROID_KEYSTORE_NAME", "ANDROID_KEYSTORE_PASS", "ANDROID_KEYALIAS_NAME", "ANDROID_KEYALIAS_PASS" },
                AndroidReleaseSettings.SigningVariables);
            Assert.AreEqual("-release", AndroidReleaseSettings.ReleaseArgument);
        }

        [Test]
        public void SigningNeedsAllFourVariables()
        {
            var complete = AndroidReleaseSettings.ReadSigning(Env(AllSigningVariables("release.keystore")), _tempRoot);
            Assert.IsTrue(complete.IsComplete);
            Assert.IsFalse(complete.IsPartial);

            var values = AllSigningVariables("release.keystore");
            values.Remove("ANDROID_KEYALIAS_PASS");
            values["ANDROID_KEYALIAS_NAME"] = "  ";
            var partial = AndroidReleaseSettings.ReadSigning(Env(values), _tempRoot);
            Assert.IsFalse(partial.IsComplete);
            Assert.IsTrue(partial.IsPartial);
            CollectionAssert.AreEquivalent(new[] { "ANDROID_KEYALIAS_NAME", "ANDROID_KEYALIAS_PASS" }, partial.Missing);

            var none = AndroidReleaseSettings.ReadSigning(Env(new Dictionary<string, string>()), _tempRoot);
            Assert.IsFalse(none.IsComplete);
            Assert.IsFalse(none.IsPartial);
            CollectionAssert.AreEqual(AndroidReleaseSettings.SigningVariables, none.Missing);
        }

        [Test]
        public void ReleaseBuildWithoutSigningVariables_Fails()
        {
            var none = AndroidReleaseSettings.ReadSigning(Env(new Dictionary<string, string>()), _tempRoot);

            var error = Assert.Throws<BuildFailedException>(() => AndroidReleaseSettings.Validate(none, release: true));
            StringAssert.Contains("ANDROID_KEYSTORE_NAME", error.Message);
            Assert.DoesNotThrow(() => AndroidReleaseSettings.Validate(none, release: false));
        }

        [Test]
        public void ReleaseBuildWithOnlyRetiredVariables_Fails_AndNamesTheReplacements()
        {
            var retired = new Dictionary<string, string>
            {
                { "KEYSTORE_PATH", Path.Combine(_tempRoot, "old.keystore") },
                { "KEYSTORE_PASS", StorePass },
                { "KEY_ALIAS", "test-alias" },
                { "KEY_PASS", KeyPass },
                { "VERSION_CODE", "42" }
            };
            var signing = AndroidReleaseSettings.ReadSigning(Env(retired), _tempRoot);
            var retiredSet = AndroidReleaseSettings.FindRetiredVariables(Env(retired));
            CollectionAssert.AreEquivalent(new[] { "KEYSTORE_PATH", "KEYSTORE_PASS", "KEY_ALIAS", "KEY_PASS", "VERSION_CODE" }, retiredSet);

            var error = Assert.Throws<BuildFailedException>(() =>
                AndroidReleaseSettings.Validate(signing, release: true, retiredSet));
            StringAssert.Contains("KEYSTORE_PATH is no longer read, use ANDROID_KEYSTORE_NAME", error.Message);
            StringAssert.Contains("KEY_PASS is no longer read, use ANDROID_KEYALIAS_PASS", error.Message);
            StringAssert.DoesNotContain(StorePass, error.Message);
            StringAssert.DoesNotContain(KeyPass, error.Message);
        }

        [Test]
        public void MissingKeystoreFile_Fails_WithoutPrintingSecrets()
        {
            var signing = AndroidReleaseSettings.ReadSigning(Env(AllSigningVariables(
                Path.Combine(_tempRoot, "does-not-exist.keystore"))), _tempRoot);

            var error = Assert.Throws<BuildFailedException>(() => AndroidReleaseSettings.Validate(signing, release: true));
            StringAssert.DoesNotContain(StorePass, error.Message);
            StringAssert.DoesNotContain(KeyPass, error.Message);
            StringAssert.DoesNotContain(_tempRoot, error.Message);
        }

        [Test]
        public void ExistingKeystoreFile_Passes()
        {
            var keystore = Path.Combine(_tempRoot, "release.p12");
            File.WriteAllBytes(keystore, new byte[] { 0 });

            var signing = AndroidReleaseSettings.ReadSigning(Env(AllSigningVariables(keystore)), _tempRoot);
            Assert.DoesNotThrow(() => AndroidReleaseSettings.Validate(signing, release: true));
            Assert.AreEqual(Path.GetFullPath(keystore), signing.KeystorePath);
        }

        [Test]
        public void RelativeKeystorePath_IsTakenFromTheProjectFolder()
        {
            // GameCI decodes ANDROID_KEYSTORE_BASE64 into <project>/<androidKeystoreName> and passes
            // ANDROID_KEYSTORE_NAME=<androidKeystoreName> into the build container.
            File.WriteAllBytes(Path.Combine(_tempRoot, "release.keystore"), new byte[] { 0 });

            var signing = AndroidReleaseSettings.ReadSigning(Env(AllSigningVariables("release.keystore")), _tempRoot);
            Assert.AreEqual(Path.GetFullPath(Path.Combine(_tempRoot, "release.keystore")), signing.KeystorePath);
            Assert.DoesNotThrow(() => AndroidReleaseSettings.Validate(signing, release: true));
        }

        [Test]
        public void ActiveBuildProfile_FailsReleaseBuildsOnly()
        {
            const string profile = "Assets/Settings/Build Profiles/Meta Quest.asset";

            Assert.DoesNotThrow(() => AndroidReleaseSettings.CheckActiveBuildProfile(null, release: true));
            Assert.DoesNotThrow(() => AndroidReleaseSettings.CheckActiveBuildProfile(profile, release: false));
            var error = Assert.Throws<BuildFailedException>(() =>
                AndroidReleaseSettings.CheckActiveBuildProfile(profile, release: true));
            StringAssert.Contains(profile, error.Message);
        }

        [TestCase("0.1.0", 1000)]
        [TestCase("1.2.3", 1002003)]
        [TestCase("2.0.15", 2000015)]
        [TestCase("12.345.678", 12345678)]
        public void VersionCode_IsMajorMinorPatch(string versionName, int expected)
        {
            Assert.AreEqual(expected, AndroidReleaseSettings.ComputeVersionCode(versionName));
        }

        [TestCase("1.2")]
        [TestCase("1.2.3.4")]
        [TestCase("1.1000.0")]
        [TestCase("1.0.1000")]
        [TestCase("v1.2.3")]
        [TestCase("0.0.0")]
        [TestCase("2101.0.0")]
        [TestCase("")]
        public void VersionCode_RejectsOtherVersionNames(string versionName)
        {
            Assert.Throws<BuildFailedException>(() => AndroidReleaseSettings.ComputeVersionCode(versionName));
        }

        [Test]
        public void ProjectVersion_GivesAVersionCode()
        {
            // Player Settings > Version is the only place the app version is kept; every build
            // derives versionCode from it, so it must stay MAJOR.MINOR.PATCH.
            Assert.DoesNotThrow(() => AndroidReleaseSettings.ComputeVersionCode(PlayerSettings.bundleVersion));
        }

        [Test]
        public void RetiredVersionVariables_AreReported()
        {
            var retired = AndroidReleaseSettings.FindRetiredVariables(Env(new Dictionary<string, string>
            {
                { "VERSION_NAME", "9.9.9" }, { "VERSION_CODE", " " }
            }));

            CollectionAssert.AreEqual(new[] { "VERSION_NAME" }, retired);
            StringAssert.Contains("bundleVersion", AndroidReleaseSettings.DescribeRetiredVariables(retired));
        }

        [Test]
        public void Apply_WithoutSigning_PutsVersionBack_AndLeavesTheKeystoreAlone()
        {
            // The release-key path is not exercised here: setting a keystore in the Editor would
            // rewrite ProjectSettings.asset (Unity stores an empty keystore as "{inproject}: ").
            var bundleVersion = PlayerSettings.bundleVersion;
            var versionCode = PlayerSettings.Android.bundleVersionCode;
            var useCustomKeystore = PlayerSettings.Android.useCustomKeystore;
            var keystoreName = PlayerSettings.Android.keystoreName;

            var unsigned = AndroidReleaseSettings.ReadSigning(Env(new Dictionary<string, string>()), _tempRoot);
            using (AndroidReleaseSettings.Apply("9.8.7", 9008007, unsigned))
            {
                Assert.AreEqual("9.8.7", PlayerSettings.bundleVersion);
                Assert.AreEqual(9008007, PlayerSettings.Android.bundleVersionCode);
                Assert.IsFalse(PlayerSettings.Android.useCustomKeystore);
            }

            Assert.AreEqual(bundleVersion, PlayerSettings.bundleVersion);
            Assert.AreEqual(versionCode, PlayerSettings.Android.bundleVersionCode);
            Assert.AreEqual(useCustomKeystore, PlayerSettings.Android.useCustomKeystore);
            Assert.AreEqual(keystoreName, PlayerSettings.Android.keystoreName);
        }

        [Test]
        public void RemoveSigningSecrets_DeletesOnlyTheFilesHoldingAPassword()
        {
            var bee = Path.Combine(_tempRoot, "Library", "Bee");
            var gradle = Path.Combine(bee, "Android", "Prj", "IL2CPP", "Gradle", "launcher");
            Directory.CreateDirectory(gradle);
            File.WriteAllText(Path.Combine(gradle, "build.gradle"), $"storePassword '{StorePass}'\nkeyAlias 'test-alias'\n");
            File.WriteAllText(Path.Combine(bee, "tundra.log.json"), $"{{\"args\":\"{KeyPass}\"}}");
            File.WriteAllText(Path.Combine(gradle, "settings.gradle"), "include ':launcher'\n");
            // A password that straddles the 1 MiB read boundary must still be found.
            var straddling = new byte[(1 << 20) + 64];
            var secret = Encoding.UTF8.GetBytes(KeyPass);
            Array.Copy(secret, 0, straddling, (1 << 20) - 4, secret.Length);
            File.WriteAllBytes(Path.Combine(bee, "Player.dag.payloads"), straddling);
            var outside = Path.Combine(_tempRoot, "Library", "outside.txt");
            File.WriteAllText(outside, StorePass);

            var signing = AndroidReleaseSettings.ReadSigning(Env(AllSigningVariables("release.keystore")), _tempRoot);
            var cleanup = AndroidReleaseSettings.RemoveSigningSecrets(signing, bee);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    Path.Combine("Android", "Prj", "IL2CPP", "Gradle", "launcher", "build.gradle"),
                    "tundra.log.json",
                    "Player.dag.payloads"
                },
                cleanup.Deleted);
            Assert.AreEqual(4, cleanup.FilesChecked);
            CollectionAssert.IsEmpty(cleanup.NotDeleted);
            CollectionAssert.IsEmpty(cleanup.NotChecked);
            Assert.IsTrue(File.Exists(Path.Combine(gradle, "settings.gradle")));
            Assert.IsFalse(File.Exists(Path.Combine(gradle, "build.gradle")));
            Assert.IsTrue(File.Exists(outside), "only the given folder is searched");
        }

        [Test]
        public void RemoveSigningSecrets_WithoutPasswords_DeletesNothing()
        {
            var bee = Path.Combine(_tempRoot, "Library", "Bee");
            Directory.CreateDirectory(bee);
            File.WriteAllText(Path.Combine(bee, "tundra.log.json"), "{}");

            var unsigned = AndroidReleaseSettings.ReadSigning(Env(new Dictionary<string, string>()), _tempRoot);
            var cleanup = AndroidReleaseSettings.RemoveSigningSecrets(unsigned, bee);

            CollectionAssert.IsEmpty(cleanup.Deleted);
            Assert.AreEqual(0, cleanup.FilesChecked);
            Assert.IsTrue(File.Exists(Path.Combine(bee, "tundra.log.json")));
            Assert.IsEmpty(AndroidReleaseSettings.RemoveSigningSecrets(unsigned, Path.Combine(_tempRoot, "missing")).Deleted);
        }
    }
}
