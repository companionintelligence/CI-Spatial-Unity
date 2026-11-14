using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using CompanionScan.Core;
using CompanionScan.Platform;
using NUnit.Framework;

namespace CompanionScan.Tests.Editor
{
    public class AndroidPermissionsTests
    {
        private const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string OvrProjectConfigPath = "Assets/Oculus/OculusProjectConfig.asset";
        private const string ScenePath = "Assets/CompanionScan/Scenes/CompanionCapture.unity";
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
        public void PassthroughCameraPermission_RequestsTheHeadsetCameraPermissionFirst()
        {
            Assert.AreEqual("horizonos.permission.HEADSET_CAMERA", PassthroughCameraPermission.HeadsetCamera);
            Assert.AreEqual(PassthroughCameraPermission.HeadsetCamera, PassthroughCameraPermission.RequiredPermissions.First(),
                "HEADSET_CAMERA gates the Quest passthrough cameras and must be requested");
            // Kept until capture is verified on a headset without it: Meta OpenXR opens the cameras
            // through the NDK Camera2 API, and WebCamTexture checks it too.
            CollectionAssert.Contains(PassthroughCameraPermission.RequiredPermissions.ToList(), "android.permission.CAMERA");
        }

        [Test]
        public void PassthroughCameraPermission_IsNotRequiredInTheEditor()
        {
            var request = PassthroughCameraPermission.RequestAsync();

            Assert.IsTrue(request.IsCompleted);
            Assert.IsTrue(request.Result);
            Assert.IsTrue(PassthroughCameraPermission.IsGranted);
            Assert.AreEqual(PassthroughCameraPermission.State.NotRequired, PassthroughCameraPermission.LastKnown);
        }

        [Test]
        public void BothCameraProviders_UseTheSharedCameraPermissions()
        {
            Assert.AreEqual(PassthroughCameraPermission.HeadsetCamera, QuestCameraProvider.HeadsetCameraPermission);
            Assert.AreEqual(PassthroughCameraPermission.AndroidCamera, QuestCameraProvider.AndroidCameraPermission);
            CollectionAssert.AreEqual(PassthroughCameraPermission.RequiredPermissions.ToList(),
                QuestCameraProvider.RequiredPermissions.ToList());
        }

        [Test]
        public void Manifest_DeclaresEveryCameraPermissionThatIsRequested()
        {
            var manifest = LoadManifest();
            foreach (var permission in PassthroughCameraPermission.RequiredPermissions)
            {
                Assert.IsNotNull(FindUsesPermission(manifest, permission, out var toolsNode), permission + " is not declared");
                Assert.IsEmpty(toolsNode, permission + " must be declared, not removed");
            }
        }

        [Test]
        public void Scene_LeavesTheHeadsetCameraRequestToTheCapturePath()
        {
            // PassthroughCameraPermission asks for HEADSET_CAMERA and CAMERA together when capture starts.
            // OVRManager's own startup request would ask for HEADSET_CAMERA a second time (twice in a row
            // for a user who denies it), so it stays off.
            var scene = File.ReadAllText(ScenePath);
            var flags = Regex.Matches(scene, @"^\s*requestPassthroughCameraAccessPermissionOnStartup:\s*(\d+)\s*$", RegexOptions.Multiline);

            Assert.AreEqual(1, flags.Count, "expected one OVRManager in " + ScenePath);
            Assert.AreEqual("0", flags[0].Groups[1].Value,
                "OVRManager must not request passthrough camera access on startup; PassthroughCameraPermission owns that request");
        }

        [TestCase("com.oculus.permission.EYE_TRACKING")]
        [TestCase("com.oculus.permission.HAND_TRACKING")]
        [TestCase("com.oculus.permission.IMPORT_EXPORT_IOT_MAP_DATA")]
        [TestCase("com.oculus.permission.USE_COLOCATION_DISCOVERY_API")]
        [TestCase("com.oculus.permission.BOUNDARY_VISIBILITY")]
        public void Manifest_RemovesUnusedMetaPermissions(string permission)
        {
            Assert.IsNotNull(FindUsesPermission(LoadManifest(), permission, out var toolsNode), permission + " has no removal entry");
            Assert.AreEqual("remove", toolsNode);
        }

        [TestCase("oculus.software.eye_tracking")]
        [TestCase("oculus.software.handtracking")]
        public void Manifest_RemovesUnusedTrackingFeatures(string name)
        {
            var manifest = LoadManifest();
            XmlElement feature = null;
            foreach (XmlElement element in manifest.SelectNodes("/manifest/uses-feature"))
            {
                if (element.GetAttribute("name", AndroidNamespace) == name)
                {
                    feature = element;
                }
            }

            Assert.IsNotNull(feature, name + " has no removal entry");
            Assert.AreEqual("remove", feature.GetAttribute("node", "http://schemas.android.com/tools"));
        }

        [Test]
        public void HandTracking_IsOffInTheMetaProjectConfig()
        {
            // OVRProjectConfig.HandTrackingSupport.ControllersOnly = 0: OVRManifestPreprocessor then adds no
            // hand tracking permission, feature or frequency entry. Every control is a controller button.
            var config = File.ReadAllText(OvrProjectConfigPath);
            StringAssert.IsMatch(@"(?m)^\s*handTrackingSupport: 0\s*$", config);
            StringAssert.DoesNotContain("com.oculus.handtracking.frequency", File.ReadAllText(ManifestPath));
        }

        [Test]
        public void Manifest_KeepsTheAnchorPermissionThatSpaceSetupQueriesNeed()
        {
            // MetaSceneCollector reads Space Setup through OVRAnchor (XR_FB_spatial_entity), and Meta XR Core adds
            // USE_ANCHOR_API whenever Scene Support is on: it must stay declared, not removed.
            Assert.IsNotNull(FindUsesPermission(LoadManifest(), "com.oculus.permission.USE_ANCHOR_API", out var toolsNode));
            Assert.IsEmpty(toolsNode, "USE_ANCHOR_API must be declared, not removed");
            StringAssert.IsMatch(@"(?m)^\s*sceneSupport: [12]\s*$", File.ReadAllText(OvrProjectConfigPath));
        }

        [Test]
        public void HandTracking_IsNotUsedByTheAppCode()
        {
            foreach (var file in Directory.GetFiles("Assets/CompanionScan", "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Tests/"))
                {
                    continue;
                }

                var source = File.ReadAllText(file);
                foreach (var api in new[]
                         {
                             "OVRHand", "OVRSkeleton", "XRHandSubsystem", "UnityEngine.XR.Hands",
                             "OVRInput.Controller.Hands", "OVRInput.Controller.LHand", "OVRInput.Controller.RHand"
                         })
                {
                    StringAssert.DoesNotContain(api, source, $"{file} uses {api}; hand tracking needs HAND_TRACKING back");
                }
            }
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
