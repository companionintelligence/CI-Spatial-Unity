using System.IO;
using CompanionScan.Editor;
using NUnit.Framework;
using UnityEditor;

namespace CompanionScan.Tests.Editor
{
    public class PerformanceTestRunFileStripperTests
    {
        [TearDown]
        public void TearDown()
        {
            // Never leave test copies behind, whatever the test did.
            PerformanceTestRunFileStripper.RemoveGeneratedFiles();
        }

        [Test]
        public void IsTestBuild_OnlyForPlayersThatIncludeTestAssemblies()
        {
            Assert.IsFalse(PerformanceTestRunFileStripper.IsTestBuild(BuildOptions.None));
            Assert.IsFalse(PerformanceTestRunFileStripper.IsTestBuild(BuildOptions.Development));
            Assert.IsTrue(PerformanceTestRunFileStripper.IsTestBuild(BuildOptions.IncludeTestAssemblies));
            Assert.IsTrue(PerformanceTestRunFileStripper.IsTestBuild(
                BuildOptions.IncludeTestAssemblies | BuildOptions.Development));
        }

        [Test]
        public void RemoveGeneratedFiles_DeletesFilesWrittenToDisk()
        {
            // TestRunBuilder writes the files with File.WriteAllText and never imports them.
            foreach (var path in PerformanceTestRunFileStripper.GeneratedFiles)
            {
                File.WriteAllText(path, "{}");
            }

            var removed = PerformanceTestRunFileStripper.RemoveGeneratedFiles();

            CollectionAssert.AreEquivalent(PerformanceTestRunFileStripper.GeneratedFiles, removed);
            foreach (var path in PerformanceTestRunFileStripper.GeneratedFiles)
            {
                Assert.IsFalse(File.Exists(path), path);
                Assert.IsFalse(File.Exists(path + ".meta"), path + ".meta");
            }
        }

        [Test]
        public void RemoveGeneratedFiles_DeletesImportedAssets()
        {
            // Another build callback may refresh the AssetDatabase before ours runs.
            foreach (var path in PerformanceTestRunFileStripper.GeneratedFiles)
            {
                File.WriteAllText(path, "{}");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Assert.IsNotEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets), path);
            }

            var removed = PerformanceTestRunFileStripper.RemoveGeneratedFiles();

            CollectionAssert.AreEquivalent(PerformanceTestRunFileStripper.GeneratedFiles, removed);
            foreach (var path in PerformanceTestRunFileStripper.GeneratedFiles)
            {
                Assert.IsEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets), path);
                Assert.IsFalse(File.Exists(path), path);
                Assert.IsFalse(File.Exists(path + ".meta"), path + ".meta");
            }
        }

        [Test]
        public void RemoveGeneratedFiles_NothingToDelete_ReturnsEmpty()
        {
            PerformanceTestRunFileStripper.RemoveGeneratedFiles();

            Assert.IsEmpty(PerformanceTestRunFileStripper.RemoveGeneratedFiles());
        }
    }
}
