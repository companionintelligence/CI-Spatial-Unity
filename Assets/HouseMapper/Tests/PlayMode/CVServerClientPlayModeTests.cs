using System;
using System.Collections;
using System.Text;
using HouseMapper.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HouseMapper.Tests.PlayMode
{
    public class CVServerClientPlayModeTests
    {
        private const string ServerUrlEnv = "HOUSEMAPPER_TEST_SERVER_URL";

        [UnityTest]
        public IEnumerator DetectEndpoint_ReturnsResponse()
        {
            var serverUrl = Environment.GetEnvironmentVariable(ServerUrlEnv);
            if (string.IsNullOrEmpty(serverUrl))
            {
                Assert.Ignore($"Set {ServerUrlEnv} to enable this test.");
            }

            var config = ScriptableObject.CreateInstance<ServerConfig>();
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
