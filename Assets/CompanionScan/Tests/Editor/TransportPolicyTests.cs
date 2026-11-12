using System.IO;
using System.Linq;
using System.Xml;
using CompanionScan.Core;
using CompanionScan.Editor;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Tests.Editor
{
    public class TransportPolicyTests
    {
        private static readonly string[] LanHosts = { "192.168.1.20", "cvbox.local" };

        [TestCase("192.168.1.20", "192.168.1.20")]
        [TestCase("10.0.0.5", "10.0.0.5")]
        [TestCase("172.16.3.4", "172.16.3.4")]
        [TestCase("172.31.255.255", "172.31.255.255")]
        [TestCase("169.254.10.10", "169.254.10.10")]
        [TestCase("127.0.0.1", "127.0.0.1")]
        [TestCase("cvbox", "cvbox")]
        [TestCase(" CVBox.Local ", "cvbox.local")]
        [TestCase("cv.home.arpa", "cv.home.arpa")]
        [TestCase("scanner.lan", "scanner.lan")]
        public void CleartextHostPolicy_AcceptsLanHosts(string host, string expected)
        {
            Assert.IsTrue(CleartextHostPolicy.TryNormalizeLanHost(host, out var normalized, out var reason), reason);
            Assert.AreEqual(expected, normalized);
        }

        [TestCase("")]
        [TestCase("8.8.8.8")]
        [TestCase("172.32.0.1")]
        [TestCase("100.64.1.2")]
        [TestCase("256.1.1.1")]
        [TestCase("192.168.1")]
        [TestCase("cv.example.com")]
        [TestCase("http://192.168.1.20")]
        [TestCase("192.168.1.20:8000")]
        [TestCase("192.168.1.20/v1")]
        [TestCase("*.local")]
        [TestCase("fe80::1")]
        [TestCase("-bad.local")]
        [TestCase(".local")]
        public void CleartextHostPolicy_RejectsNonLanHosts(string host)
        {
            Assert.IsFalse(CleartextHostPolicy.TryNormalizeLanHost(host, out _, out var reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void CheckUrlPolicy_AllowsHttpsInEveryBuild()
        {
            foreach (var development in new[] { false, true })
            {
                foreach (var enforce in new[] { false, true })
                {
                    Assert.IsTrue(ServerConfig.CheckUrlPolicy("https://cv.example.com", development, enforce, null, out var error), error);
                }
            }
        }

        [Test]
        public void CheckUrlPolicy_BlocksHttpInReleaseBuilds()
        {
            Assert.IsFalse(ServerConfig.CheckUrlPolicy("http://192.168.1.20:8000", false, true, LanHosts, out var error));
            StringAssert.Contains("release", error);
        }

        [Test]
        public void CheckUrlPolicy_AllowsHttpOnlyToListedLanHostsInDevelopmentBuilds()
        {
            Assert.IsTrue(ServerConfig.CheckUrlPolicy("http://192.168.1.20:8000", true, true, LanHosts, out var error), error);
            Assert.IsTrue(ServerConfig.CheckUrlPolicy("http://CVBOX.local:8000", true, true, LanHosts, out error), error);
            Assert.IsFalse(ServerConfig.CheckUrlPolicy("http://192.168.1.21:8000", true, true, LanHosts, out error));
            Assert.IsFalse(ServerConfig.CheckUrlPolicy("http://cv.example.com", true, true, new[] { "cv.example.com" }, out error));
        }

        [Test]
        public void CheckUrlPolicy_DoesNotEnforceTheAllowListOutsideAndroid()
        {
            Assert.IsTrue(ServerConfig.CheckUrlPolicy("http://localhost:8000", true, false, null, out var error), error);
        }

        [TestCase("ftp://cv.example.com")]
        [TestCase("not a url")]
        [TestCase(null)]
        public void CheckUrlPolicy_RejectsInvalidUrls(string url)
        {
            Assert.IsFalse(ServerConfig.CheckUrlPolicy(url, true, false, null, out var error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void ShippedServerConfigListsNoCleartextHosts()
        {
            var asset = Resources.Load<ServerConfig>(ServerConfig.ResourcesPath);

            Assert.IsNotNull(asset);
            Assert.IsEmpty(asset.developmentCleartextHosts);
        }

        [Test]
        public void NetworkSecurityConfig_ReleaseForbidsAllCleartext()
        {
            var document = Parse(AndroidNetworkSecurityConfig.BuildXml(new string[0]));

            Assert.AreEqual("false", document.SelectSingleNode("/network-security-config/base-config/@cleartextTrafficPermitted")?.Value);
            Assert.IsNull(document.SelectSingleNode("//domain-config"));
        }

        [Test]
        public void NetworkSecurityConfig_DevelopmentAllowsOnlyValidLanHosts()
        {
            var xml = AndroidNetworkSecurityConfig.BuildXml(new[] { "192.168.1.20", "CVBox.local", "8.8.8.8", "100.64.1.2", "http://10.0.0.1" });
            var document = Parse(xml);

            Assert.AreEqual("false", document.SelectSingleNode("/network-security-config/base-config/@cleartextTrafficPermitted")?.Value);
            Assert.AreEqual("true", document.SelectSingleNode("//domain-config/@cleartextTrafficPermitted")?.Value);
            var domains = document.SelectNodes("//domain-config/domain").Cast<XmlNode>().Select(node => node.InnerText).ToArray();
            CollectionAssert.AreEqual(new[] { "192.168.1.20", "cvbox.local" }, domains);
        }

        [Test]
        public void NetworkSecurityConfig_SetsTheManifestReference()
        {
            var path = Path.Combine(Path.GetTempPath(), "CompanionScanManifest-" + System.Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                File.WriteAllText(path,
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                    "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\">\n" +
                    "  <application android:label=\"x\" />\n" +
                    "</manifest>\n");

                Assert.IsTrue(AndroidNetworkSecurityConfig.SetManifestReference(path));

                var document = new XmlDocument();
                document.Load(path);
                var namespaces = new XmlNamespaceManager(document.NameTable);
                namespaces.AddNamespace("android", "http://schemas.android.com/apk/res/android");
                Assert.AreEqual(AndroidNetworkSecurityConfig.ManifestValue,
                    document.SelectSingleNode("/manifest/application/@android:networkSecurityConfig", namespaces)?.Value);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void CustomManifestReferencesTheGeneratedConfig()
        {
            StringAssert.Contains(
                "android:networkSecurityConfig=\"" + AndroidNetworkSecurityConfig.ManifestValue + "\"",
                File.ReadAllText("Assets/Plugins/Android/AndroidManifest.xml"));
        }

        private static XmlDocument Parse(string xml)
        {
            var document = new XmlDocument();
            document.LoadXml(xml);
            return document;
        }
    }
}
