using System;
using System.IO;
using CompanionScan.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;

namespace CompanionScan.Tests.Editor
{
    /// <summary>The CV server requires a bearer token on every route; the app sends the one entered in-headset.</summary>
    public class ServerApiTokenTests
    {
        private const string ServerConfigAssetPath = "Assets/CompanionScan/Resources/ServerConfig.asset";

        private bool _hadToken;
        private string _savedToken;
        private string _savedEnv;
        private ServerConfig _config;

        [SetUp]
        public void SetUp()
        {
            _hadToken = PlayerPrefs.HasKey(ServerConfig.PlayerPrefsApiTokenKey);
            _savedToken = PlayerPrefs.GetString(ServerConfig.PlayerPrefsApiTokenKey, string.Empty);
            PlayerPrefs.DeleteKey(ServerConfig.PlayerPrefsApiTokenKey);

            _savedEnv = Environment.GetEnvironmentVariable(ServerConfig.EditorApiTokenEnvVar);
            Environment.SetEnvironmentVariable(ServerConfig.EditorApiTokenEnvVar, null);

            _config = ScriptableObject.CreateInstance<ServerConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_config);

            if (_hadToken)
            {
                PlayerPrefs.SetString(ServerConfig.PlayerPrefsApiTokenKey, _savedToken);
            }
            else
            {
                PlayerPrefs.DeleteKey(ServerConfig.PlayerPrefsApiTokenKey);
            }

            PlayerPrefs.Save();
            Environment.SetEnvironmentVariable(ServerConfig.EditorApiTokenEnvVar, _savedEnv);
        }

        [Test]
        public void NoTokenByDefault_AndNoHeaderIsSent()
        {
            Assert.AreEqual(string.Empty, _config.ResolveApiToken());
            Assert.IsFalse(_config.HasApiToken);

            using var request = new UnityWebRequest("https://cv.example.com/v1/detect", "POST");
            _config.ApplyAuthorization(request);
            Assert.IsNull(request.GetRequestHeader("Authorization"));
        }

        [Test]
        public void SavedToken_IsSentAsBearer()
        {
            _config.SaveApiTokenOverride("  token-from-headset  ");

            Assert.AreEqual("token-from-headset", _config.ResolveApiToken());
            using var request = new UnityWebRequest("https://cv.example.com/v1/detect", "POST");
            _config.ApplyAuthorization(request);
            Assert.AreEqual("Bearer token-from-headset", request.GetRequestHeader("Authorization"));
        }

        [Test]
        public void EmptyToken_RemovesTheOverride()
        {
            _config.SaveApiTokenOverride("token-from-headset");
            _config.SaveApiTokenOverride("   ");

            Assert.IsFalse(PlayerPrefs.HasKey(ServerConfig.PlayerPrefsApiTokenKey));
            Assert.IsFalse(_config.HasApiToken);
        }

        [Test]
        public void EditorEnvironmentVariable_IsUsedWhenNothingIsSaved()
        {
            Environment.SetEnvironmentVariable(ServerConfig.EditorApiTokenEnvVar, "token-from-env");
            Assert.AreEqual("token-from-env", _config.ResolveApiToken());

            _config.SaveApiTokenOverride("token-from-headset");
            Assert.AreEqual("token-from-headset", _config.ResolveApiToken());
        }

        [Test]
        public void ShippedAsset_HoldsNoToken()
        {
            // The token lives in PlayerPrefs only; the asset has no field for it.
            StringAssert.DoesNotContain("token", File.ReadAllText(ServerConfigAssetPath).ToLowerInvariant());
        }

        [Test]
        public void AuthFailures_ExplainWhatToDo()
        {
            StringAssert.Contains("API token", ServerConfig.DescribeServerError(401, "HTTP/1.1 401 Unauthorized"));
            StringAssert.Contains("CV_SERVER_API_TOKEN", ServerConfig.DescribeServerError(503, "HTTP/1.1 503"));
            StringAssert.Contains("(500)", ServerConfig.DescribeServerError(500, "HTTP/1.1 500"));
        }
    }
}
