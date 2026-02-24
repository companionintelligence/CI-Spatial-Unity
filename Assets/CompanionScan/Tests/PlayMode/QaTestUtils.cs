using System;
using System.IO;
using UnityEngine;

namespace CompanionScan.Tests.PlayMode
{
    internal static class QaTestUtils
    {
        private const string ArtifactsEnv = "CompanionScan_TEST_ARTIFACTS";

        public static string GetArtifactsDir()
        {
            var env = Environment.GetEnvironmentVariable(ArtifactsEnv);
            var dir = string.IsNullOrWhiteSpace(env)
                ? Path.Combine(Application.persistentDataPath, "CompanionScanTestArtifacts")
                : env;

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return dir;
        }

        public static string CaptureScreenshotPng(string fileName)
        {
            var dir = GetArtifactsDir();
            var safeName = string.IsNullOrWhiteSpace(fileName) ? "screenshot" : fileName;
            var path = Path.Combine(dir, safeName + ".png");

            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null)
            {
                return path;
            }

            var bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            UnityEngine.Object.Destroy(tex);

            return path;
        }
    }
}
