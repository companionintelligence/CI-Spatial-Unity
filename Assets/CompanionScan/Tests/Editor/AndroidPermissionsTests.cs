using System.Xml;
using CompanionScan.Core;
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
