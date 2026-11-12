using System;
using System.Collections.Generic;
using System.IO;
using CompanionScan.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace CompanionScan.Tests.Editor
{
    public class AndroidReleaseSettingsTests
    {
        private static Func<string, string> Env(Dictionary<string, string> values) =>
            name => values.TryGetValue(name, out var value) ? value : null;

        private static Dictionary<string, string> AllSigningVariables(string keystorePath) => new()
        {
            { "KEYSTORE_PATH", keystorePath },
            { "KEYSTORE_PASS", "test-store-pass" },
            { "KEY_ALIAS", "test-alias" },
            { "KEY_PASS", "test-key-pass" }
        };

        [Test]
        public void SigningNeedsAllFourVariables()
        {
            var complete = AndroidReleaseSettings.ReadSigning(Env(AllSigningVariables("release.keystore")));
            Assert.IsTrue(complete.IsComplete);
            Assert.IsFalse(complete.IsPartial);

            var values = AllSigningVariables("release.keystore");
            values.Remove("KEY_PASS");
            values["KEY_ALIAS"] = "  ";
            var partial = AndroidReleaseSettings.ReadSigning(Env(values));
            Assert.IsFalse(partial.IsComplete);
            Assert.IsTrue(partial.IsPartial);
            CollectionAssert.AreEquivalent(new[] { "KEY_ALIAS", "KEY_PASS" }, partial.Missing);

            var none = AndroidReleaseSettings.ReadSigning(Env(new Dictionary<string, string>()));
            Assert.IsFalse(none.IsComplete);
            Assert.IsFalse(none.IsPartial);
            CollectionAssert.AreEqual(AndroidReleaseSettings.SigningVariables, none.Missing);
        }

        [Test]
        public void ReleaseBuildWithoutSigningVariables_Fails()
        {
            var none = AndroidReleaseSettings.ReadSigning(Env(new Dictionary<string, string>()));

            var error = Assert.Throws<BuildFailedException>(() => AndroidReleaseSettings.Validate(none, release: true));
            StringAssert.Contains("KEYSTORE_PATH", error.Message);
            Assert.DoesNotThrow(() => AndroidReleaseSettings.Validate(none, release: false));
        }

        [Test]
        public void MissingKeystoreFile_Fails_WithoutPrintingSecrets()
        {
            var signing = AndroidReleaseSettings.ReadSigning(Env(AllSigningVariables(
                Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".keystore"))));

            var error = Assert.Throws<BuildFailedException>(() => AndroidReleaseSettings.Validate(signing, release: true));
            StringAssert.DoesNotContain("test-store-pass", error.Message);
            StringAssert.DoesNotContain("test-key-pass", error.Message);
        }

        [Test]
        public void ExistingKeystoreFile_Passes()
        {
            var keystore = Path.GetTempFileName();
            try
            {
                var signing = AndroidReleaseSettings.ReadSigning(Env(AllSigningVariables(keystore)));
                Assert.DoesNotThrow(() => AndroidReleaseSettings.Validate(signing, release: true));
            }
            finally
            {
                File.Delete(keystore);
            }
        }

        [TestCase("0.1.0", 1000)]
        [TestCase("1.2.3", 1002003)]
        [TestCase("2.0.15", 2000015)]
        [TestCase("12.345.678", 12345678)]
        public void VersionCode_IsMajorMinorPatch(string versionName, int expected)
        {
            Assert.AreEqual(expected, AndroidReleaseSettings.ComputeVersionCode(versionName));
            Assert.AreEqual(expected, AndroidReleaseSettings.ResolveVersionCode(Env(new Dictionary<string, string>()), versionName));
        }

        [TestCase("1.2")]
        [TestCase("1.2.3.4")]
        [TestCase("1.1000.0")]
        [TestCase("1.0.1000")]
        [TestCase("v1.2.3")]
        [TestCase("0.0.0")]
        [TestCase("")]
        public void VersionCode_RejectsOtherVersionNames(string versionName)
        {
            Assert.Throws<BuildFailedException>(() => AndroidReleaseSettings.ComputeVersionCode(versionName));
        }

        [Test]
        public void VersionCodeVariable_WinsAndIsValidated()
        {
            Assert.AreEqual(42, AndroidReleaseSettings.ResolveVersionCode(
                Env(new Dictionary<string, string> { { "VERSION_CODE", "42" } }), "not-a-version"));

            foreach (var bad in new[] { "0", "-5", "abc", "2100000001" })
            {
                Assert.Throws<BuildFailedException>(() => AndroidReleaseSettings.ResolveVersionCode(
                    Env(new Dictionary<string, string> { { "VERSION_CODE", bad } }), "0.1.0"), bad);
            }
        }

        [Test]
        public void VersionNameVariable_OverridesBundleVersion()
        {
            Assert.AreEqual("0.1.0", AndroidReleaseSettings.ResolveVersionName(Env(new Dictionary<string, string>()), "0.1.0"));
            Assert.AreEqual("0.2.0", AndroidReleaseSettings.ResolveVersionName(
                Env(new Dictionary<string, string> { { "VERSION_NAME", " 0.2.0 " } }), "0.1.0"));
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

            var unsigned = AndroidReleaseSettings.ReadSigning(Env(new Dictionary<string, string>()));
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
    }
}
