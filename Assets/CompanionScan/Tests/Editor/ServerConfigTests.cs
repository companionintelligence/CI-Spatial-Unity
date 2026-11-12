using System;
using System.IO;
using CompanionScan.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CompanionScan.Tests.Editor
{
    public class ServerConfigTests
    {
        private const string MainScenePath = "Assets/CompanionScan/Scenes/CompanionCapture.unity";

        private bool _hadOverride;
        private string _savedOverride;
        private string _savedEnv;
        private ServerConfig _config;

        [SetUp]
        public void SetUp()
        {
            _hadOverride = PlayerPrefs.HasKey(ServerConfig.PlayerPrefsBaseUrlKey);
            _savedOverride = PlayerPrefs.GetString(ServerConfig.PlayerPrefsBaseUrlKey, string.Empty);
            PlayerPrefs.DeleteKey(ServerConfig.PlayerPrefsBaseUrlKey);

            _savedEnv = Environment.GetEnvironmentVariable(ServerConfig.EditorServerUrlEnvVar);
            Environment.SetEnvironmentVariable(ServerConfig.EditorServerUrlEnvVar, null);

            _config = ScriptableObject.CreateInstance<ServerConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_config);

            if (_hadOverride)
            {
                PlayerPrefs.SetString(ServerConfig.PlayerPrefsBaseUrlKey, _savedOverride);
            }
            else
            {
                PlayerPrefs.DeleteKey(ServerConfig.PlayerPrefsBaseUrlKey);
            }

            PlayerPrefs.Save();
            Environment.SetEnvironmentVariable(ServerConfig.EditorServerUrlEnvVar, _savedEnv);
        }

        [Test]
        public void ShippedAssetHasNoServerUrl()
        {
            var asset = Resources.Load<ServerConfig>(ServerConfig.ResourcesPath);

            Assert.IsNotNull(asset, "ServerConfig asset missing from Resources");
            Assert.IsEmpty(asset.baseUrl, "The repository must not ship a default server URL");
        }

        [Test]
        public void ResolveBaseUrl_IsEmptyWhenNothingIsConfigured()
        {
            Assert.AreEqual(string.Empty, _config.ResolveBaseUrl());
            Assert.IsFalse(_config.HasBaseUrl);
        }

        [Test]
        public void ResolveBaseUrl_PrefersThePlayerPrefsOverride()
        {
            _config.baseUrl = "https://default.example.com";
            Environment.SetEnvironmentVariable(ServerConfig.EditorServerUrlEnvVar, "https://env.example.com");
            _config.SaveBaseUrlOverride("https://override.example.com");

            Assert.AreEqual("https://override.example.com", _config.ResolveBaseUrl());
        }

        [Test]
        public void ResolveBaseUrl_UsesTheEditorEnvironmentVariableBeforeTheAssetDefault()
        {
            _config.baseUrl = "https://default.example.com";
            Environment.SetEnvironmentVariable(ServerConfig.EditorServerUrlEnvVar, " https://env.example.com ");

            Assert.AreEqual("https://env.example.com", _config.ResolveBaseUrl());
        }

        [Test]
        public void ClearBaseUrlOverride_FallsBackToTheAssetDefault()
        {
            _config.baseUrl = "https://default.example.com";
            _config.SaveBaseUrlOverride("https://override.example.com");
            _config.ClearBaseUrlOverride();

            Assert.AreEqual("https://default.example.com", _config.ResolveBaseUrl());
        }

        [TestCase("cv.example.com", "https://cv.example.com")]
        [TestCase("  https://cv.example.com:8443/  ", "https://cv.example.com:8443")]
        [TestCase("https://cv.example.com/api/", "https://cv.example.com/api")]
        [TestCase("http://192.168.1.20:8000", "http://192.168.1.20:8000")]
        public void TryNormalizeBaseUrl_AcceptsAndDefaultsToHttps(string input, string expected)
        {
            Assert.IsTrue(ServerConfig.TryNormalizeBaseUrl(input, out var normalized, out var error), error);
            Assert.AreEqual(expected, normalized);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("https://")]
        [TestCase("ftp://cv.example.com")]
        [TestCase("https://user:pass@cv.example.com")]
        [TestCase("https://cv.example.com/?token=x")]
        [TestCase("https://cv.example.com/#section")]
        public void TryNormalizeBaseUrl_RejectsInvalidInput(string input)
        {
            Assert.IsFalse(ServerConfig.TryNormalizeBaseUrl(input, out _, out var error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void MainSceneContainsTheSettingsPanel()
        {
            var scriptGuid = AssetDatabase.AssetPathToGUID("Assets/CompanionScan/UI/SettingsPanel.cs");

            Assert.IsNotEmpty(scriptGuid);
            StringAssert.Contains("guid: " + scriptGuid, File.ReadAllText(MainScenePath));
        }
    }
}
