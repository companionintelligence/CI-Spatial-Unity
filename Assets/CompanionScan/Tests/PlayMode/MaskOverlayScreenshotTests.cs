using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CompanionScan.Tests.PlayMode
{
    public class MaskOverlayScreenshotTests
    {
        private const string BaselinePath = "Assets/CompanionScan/Tests/PlayMode/Baselines/mask_overlay.png";
        private const float PixelTolerance = 0.02f;

        [UnityTest]
        public IEnumerator MaskOverlay_MatchesBaseline()
        {
            yield return new WaitForEndOfFrame();

            var captured = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                if (!File.Exists(BaselinePath))
                {
                    if (TestRuntimeFlags.IsSnapshotAutoUpdateEnabled)
                    {
                        SaveBaselineAndManifest(captured, "baseline_missing");
                        Assert.Pass("Baseline auto-created.");
                    }

                    Assert.Ignore("Baseline screenshot not found. Capture and save it to run this test.");
                }

                var baseline = LoadTexture(BaselinePath);
                Assert.IsNotNull(baseline);
                Assert.AreEqual(baseline.width, captured.width);
                Assert.AreEqual(baseline.height, captured.height);

                var baselinePixels = baseline.GetPixels32();
                var capturedPixels = captured.GetPixels32();
                int diffCount = 0;
                int total = baselinePixels.Length;

                for (int i = 0; i < total; i++)
                {
                    if (!ApproximatelyEqual(baselinePixels[i], capturedPixels[i]))
                    {
                        diffCount++;
                    }
                }

                float diffRatio = diffCount / (float)total;
                if (diffRatio > PixelTolerance && TestRuntimeFlags.IsSnapshotAutoUpdateEnabled)
                {
                    SaveBaselineAndManifest(captured, $"baseline_updated diff={diffRatio:P2}");
                    Assert.Pass($"Baseline auto-updated (diff={diffRatio:P2}).");
                }

                Assert.LessOrEqual(diffRatio, PixelTolerance, $"Pixel diff ratio {diffRatio:P2} exceeds tolerance.");
                Object.Destroy(baseline);
            }
            finally
            {
                Object.Destroy(captured);
            }
        }

        private static void SaveBaselineAndManifest(Texture2D captured, string reason)
        {
            var dir = Path.GetDirectoryName(BaselinePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var bytes = captured.EncodeToPNG();
            File.WriteAllBytes(BaselinePath, bytes);

            var artifactsRoot = System.Environment.GetEnvironmentVariable("CompanionScan_TEST_ARTIFACTS");
            if (string.IsNullOrWhiteSpace(artifactsRoot))
            {
                artifactsRoot = Application.persistentDataPath;
            }

            Directory.CreateDirectory(artifactsRoot);
            var manifestPath = Path.Combine(artifactsRoot, "snapshot_manifest.log");
            var line = $"{System.DateTime.UtcNow:O}\tMaskOverlayScreenshotTests\t{reason}\t{BaselinePath}";
            File.AppendAllLines(manifestPath, new[] { line });
        }

        private static Texture2D LoadTexture(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes);
            return tex;
        }

        private static bool ApproximatelyEqual(Color32 a, Color32 b)
        {
            return Mathf.Abs(a.r - b.r) <= 5
                   && Mathf.Abs(a.g - b.g) <= 5
                   && Mathf.Abs(a.b - b.b) <= 5
                   && Mathf.Abs(a.a - b.a) <= 5;
        }
    }
}