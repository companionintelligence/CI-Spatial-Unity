using System;
using System.Collections;
using CompanionScan.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CompanionScan.Tests.PlayMode
{
    public class CVServerClientPlayModeTests
    {
        private const string ServerUrlEnv = "CompanionScan_TEST_SERVER_URL";

        [UnityTest]
        public IEnumerator DetectEndpoint_ReturnsResponse()
        {
            TestRuntimeFlags.RequireLiveIntegrationOrIgnore("Live CV-server test disabled");

            var config = ScriptableObject.CreateInstance<ServerConfig>();
            var serverUrl = Environment.GetEnvironmentVariable(ServerUrlEnv);
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                serverUrl = config.ResolveBaseUrl();
            }

            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                Assert.Ignore($"Set {ServerUrlEnv} or assign ServerConfig.baseUrl to enable this test.");
            }

            config.baseUrl = serverUrl;

            var client = new CVServerClient(config);
            var jpeg = BuildTestJpeg();

            var task = client.DetectAsync(jpeg, "chair . table .");
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                Assert.Fail(task.Exception?.GetBaseException().Message);
            }

            var result = task.Result;
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.labels);
        }

        private static byte[] BuildTestJpeg()
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGB24, false);
            var pixels = new Color32[64 * 64];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex.EncodeToJPG(80);
        }
    }
}