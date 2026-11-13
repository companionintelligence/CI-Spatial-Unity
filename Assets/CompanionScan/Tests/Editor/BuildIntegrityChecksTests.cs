using System.IO;
using System.IO.Compression;
using CompanionScan.Editor;
using NUnit.Framework;
using UnityEditor.Build;

namespace CompanionScan.Tests.Editor
{
    public class BuildIntegrityChecksTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "CompanionScanBuildChecks_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        private string MakePackage(string name, params string[] entries)
        {
            var path = Path.Combine(_tempDir, name);
            using (var stream = File.Create(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var entry in entries)
                {
                    using var writer = new StreamWriter(archive.CreateEntry(entry).Open());
                    writer.Write("x");
                }
            }

            return path;
        }

        [Test]
        public void ApkWithBurstCode_Passes()
        {
            var apk = MakePackage("ok.apk", "lib/arm64-v8a/libil2cpp.so", "lib/arm64-v8a/lib_burst_generated.so");

            CollectionAssert.IsEmpty(BuildIntegrityChecks.FindAbisWithoutBurstCode(apk, new[] { "arm64-v8a" }));
            Assert.DoesNotThrow(() => BuildIntegrityChecks.VerifyBurstCode(apk, new[] { "arm64-v8a" }));
        }

        [Test]
        public void AppBundleWithBurstCode_Passes()
        {
            var aab = MakePackage("ok.aab", "base/lib/arm64-v8a/lib_burst_generated.so");

            CollectionAssert.IsEmpty(BuildIntegrityChecks.FindAbisWithoutBurstCode(aab, new[] { "arm64-v8a" }));
        }

        [Test]
        public void ApkWithoutBurstCode_FailsTheBuild()
        {
            // The 6000.6 upgrade branch's APK: IL2CPP code but no Burst library.
            var apk = MakePackage("noburst.apk", "lib/arm64-v8a/libil2cpp.so", "lib/arm64-v8a/libunity.so");

            CollectionAssert.AreEqual(new[] { "arm64-v8a" },
                BuildIntegrityChecks.FindAbisWithoutBurstCode(apk, new[] { "arm64-v8a" }));
            Assert.Throws<BuildFailedException>(() => BuildIntegrityChecks.VerifyBurstCode(apk, new[] { "arm64-v8a" }));
        }

        [Test]
        public void BurstIsOnForAndroid_AndTheQuestBuildIsArm64()
        {
            Assert.IsTrue(BuildIntegrityChecks.IsBurstEnabledForAndroid());
            CollectionAssert.Contains(BuildIntegrityChecks.AndroidAbis(), "arm64-v8a");
        }

        [Test]
        public void EveryBuildCallbackCanBeCreated()
        {
            // A constructor that throws here makes Unity skip later build callbacks, Burst included.
            CollectionAssert.IsEmpty(BuildIntegrityChecks.FindThrowingBuildCallbacks());
        }
    }
}
