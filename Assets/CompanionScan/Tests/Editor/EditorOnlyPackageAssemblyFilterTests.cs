using CompanionScan.Editor;
using NUnit.Framework;

namespace CompanionScan.Tests.Editor
{
    public class EditorOnlyPackageAssemblyFilterTests
    {
        [Test]
        public void Filter_RemovesMcpForUnityAssemblies_KeepsEverythingElse()
        {
            var input = new[]
            {
                "Library/PlayerScriptAssemblies/CompanionScan.dll",
                "Library/PlayerScriptAssemblies/MCPForUnity.Runtime.dll",
                "Library/PlayerScriptAssemblies/Unity.TextMeshPro.dll",
                @"C:\Project\Temp\StagingArea\Data\Managed\MCPForUnity.dll",
            };

            var kept = EditorOnlyPackageAssemblyFilter.Filter(input, out var removed);

            CollectionAssert.AreEqual(new[] { input[0], input[2] }, kept);
            CollectionAssert.AreEqual(new[] { input[1], input[3] }, removed);
        }

        [Test]
        public void IsExcluded_MatchesOnlyTheExcludedAssemblyFamily()
        {
            Assert.IsTrue(EditorOnlyPackageAssemblyFilter.IsExcluded("MCPForUnity.Runtime.dll"));
            Assert.IsTrue(EditorOnlyPackageAssemblyFilter.IsExcluded("mcpforunity.runtime.dll"));
            Assert.IsFalse(EditorOnlyPackageAssemblyFilter.IsExcluded("MCPForUnityLookalike.dll"));
            Assert.IsFalse(EditorOnlyPackageAssemblyFilter.IsExcluded("CompanionScan.dll"));
            Assert.IsFalse(EditorOnlyPackageAssemblyFilter.IsExcluded(null));
            Assert.IsFalse(EditorOnlyPackageAssemblyFilter.IsExcluded(string.Empty));
        }

        [Test]
        public void Filter_NullInput_ReturnsEmpty()
        {
            var kept = EditorOnlyPackageAssemblyFilter.Filter(null, out var removed);

            Assert.IsEmpty(kept);
            Assert.IsEmpty(removed);
        }
    }
}
