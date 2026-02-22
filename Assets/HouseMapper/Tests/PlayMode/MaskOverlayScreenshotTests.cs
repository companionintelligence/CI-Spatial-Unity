using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HouseMapper.Tests.PlayMode
{
    public class MaskOverlayScreenshotTests
    {
        private const string BaselinePath = "Assets/HouseMapper/Tests/PlayMode/Baselines/mask_overlay.png";
        private const float PixelTolerance = 0.02f;

        [UnityTest]
        public IEnumerator MaskOverlay_MatchesBaseline()
        {
            if (!File.Exists(BaselinePath))
            {
                Assert.Ignore("Baseline screenshot not found. Capture and save it to run this test.");
            }

            yield return new WaitForEndOfFrame();
            var captured = ScreenCapture.CaptureScreenshotAsTexture();
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
            Assert.LessOrEqual(diffRatio, PixelTolerance, $"Pixel diff ratio {diffRatio:P2} exceeds tolerance.");

            Object.Destroy(captured);
            Object.Destroy(baseline);
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
