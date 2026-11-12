using System.Linq;
using System.Xml;
using CompanionScan.Core;
using CompanionScan.Platform;
using NUnit.Framework;

namespace CompanionScan.Tests.Editor
{
    public class AndroidPermissionsTests
    {
        private const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

        [Test]
        public void SpatialDataPermission_IsNotRequiredInTheEditor()
        {
            var request = SpatialDataPermission.RequestAsync();

            Assert.IsTrue(request.IsCompleted);
            Assert.IsTrue(request.Result);
            Assert.IsTrue(SpatialDataPermission.IsGranted);
            Assert.AreEqual(SpatialDataPermission.State.NotRequired, SpatialDataPermission.LastKnown);
        }

        [Test]
        public void Manifest_DeclaresTheSpatialDataPermission()
        {
            Assert.IsNotNull(FindUsesPermission(LoadManifest(), SpatialDataPermission.PermissionId, out var toolsNode));
            Assert.IsEmpty(toolsNode, "USE_SCENE must be declared, not removed");
        }

        [Test]
        public void QuestCameraProvider_RequestsTheHeadsetCameraPermission()
        {
            Assert.AreEqual("horizonos.permission.HEADSET_CAMERA", QuestCameraProvider.HeadsetCameraPermission);
            Assert.AreEqual(QuestCameraProvider.HeadsetCameraPermission, QuestCameraProvider.RequiredPermissions.First(),
                "HEADSET_CAMERA gates the Quest passthrough cameras and must be requested");
            // Kept until capture is verified on a headset without it: WebCamTexture checks it.
            CollectionAssert.Contains(QuestCameraProvider.RequiredPermissions.ToList(), "android.permission.CAMERA");
        }

        [Test]
        public void Manifest_DeclaresEveryCameraPermissionTheProviderRequests()
        {
            var manifest = LoadManifest();
            foreach (var permission in QuestCameraProvider.RequiredPermissions)
            {
                Assert.IsNotNull(FindUsesPermission(manifest, permission, out var toolsNode), permission + " is not declared");
                Assert.IsEmpty(toolsNode, permission + " must be declared, not removed");
            }
        }

        [TestCase("com.oculus.permission.EYE_TRACKING")]
        [TestCase("com.oculus.permission.IMPORT_EXPORT_IOT_MAP_DATA")]
        [TestCase("com.oculus.permission.USE_COLOCATION_DISCOVERY_API")]
        [TestCase("com.oculus.permission.BOUNDARY_VISIBILITY")]
        public void Manifest_RemovesUnusedMetaPermissions(string permission)
        {
            Assert.IsNotNull(FindUsesPermission(LoadManifest(), permission, out var toolsNode), permission + " has no removal entry");
            Assert.AreEqual("remove", toolsNode);
        }

        [Test]
        public void Manifest_RemovesTheEyeTrackingFeature()
        {
            var manifest = LoadManifest();
            XmlElement feature = null;
            foreach (XmlElement element in manifest.SelectNodes("/manifest/uses-feature"))
            {
                if (element.GetAttribute("name", AndroidNamespace) == "oculus.software.eye_tracking")
                {
                    feature = element;
                }
            }

            Assert.IsNotNull(feature);
            Assert.AreEqual("remove", feature.GetAttribute("node", "http://schemas.android.com/tools"));
        }

        private static XmlDocument LoadManifest()
        {
            var document = new XmlDocument();
            document.Load(ManifestPath);
            return document;
        }

        /// <summary>Finds a uses-permission entry and reports its tools:node value (empty when absent).</summary>
        internal static XmlElement FindUsesPermission(XmlDocument manifest, string name, out string toolsNode)
        {
            foreach (XmlElement element in manifest.SelectNodes("/manifest/uses-permission"))
            {
                if (element.GetAttribute("name", AndroidNamespace) == name)
                {
                    toolsNode = element.GetAttribute("node", "http://schemas.android.com/tools");
                    return element;
                }
            }

            toolsNode = string.Empty;
            return null;
        }
    }
}
