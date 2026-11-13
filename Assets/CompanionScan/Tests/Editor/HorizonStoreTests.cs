using CompanionScan.Platform;
using NUnit.Framework;

namespace CompanionScan.Tests.Editor
{
    public class HorizonStoreTests
    {
        [Test]
        public void StoreConfigShipsInResourcesAndQuitsWhenNotEntitled()
        {
            var config = HorizonStoreConfig.Load();

            Assert.IsNotNull(config, "Resources/" + HorizonStoreConfig.ResourcesPath + " is missing");
            Assert.IsTrue(config.quitWhenNotEntitled);
        }

        [Test]
        public void EntitlementCheckDoesNotRunInEditMode()
        {
            Assert.AreEqual(HorizonStoreEntitlementCheck.Result.NotChecked, HorizonStoreEntitlementCheck.Status);
        }
    }
}
