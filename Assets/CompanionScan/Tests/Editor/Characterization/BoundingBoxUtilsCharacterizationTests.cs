using CompanionScan.Core;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// Edge cases that the PlayMode BoundingBoxUtilsTests (already normalized, pixels, reversed,
    /// clamped, invalid size) do not cover. A test with "Currently" in its name records a result
    /// that looks doubtful, not a requirement.
    /// </summary>
    public class BoundingBoxUtilsCharacterizationTests
    {
        // ---- TryNormalize ----

        [Test]
        public void TryNormalize_ReturnsFalseForNoBoxOrFewerThanFourValues()
        {
            Assert.IsFalse(BoundingBoxUtils.TryNormalize(null, 640, 480, out var rect));
            Assert.AreEqual(Rect.zero, rect);
            Assert.IsFalse(BoundingBoxUtils.TryNormalize(new float[0], 640, 480, out rect));
            Assert.IsFalse(BoundingBoxUtils.TryNormalize(new[] { 0.1f, 0.2f, 0.3f }, 640, 480, out rect));
            Assert.AreEqual(Rect.zero, rect);
        }

        [Test]
        public void TryNormalize_IgnoresValuesAfterTheFourth()
        {
            Assert.IsTrue(BoundingBoxUtils.TryNormalize(new[] { 0.1f, 0.2f, 0.4f, 0.6f, 99f, 99f }, 640, 480, out var rect));

            AssertRect(new Rect(0.1f, 0.2f, 0.3f, 0.4f), rect);
        }

        [Test]
        public void TryNormalize_AcceptsANormalizedBoxWhateverTheImageSize()
        {
            Assert.IsTrue(BoundingBoxUtils.TryNormalize(new[] { 0.1f, 0.2f, 0.4f, 0.6f }, 0, 0, out var rect));

            AssertRect(new Rect(0.1f, 0.2f, 0.3f, 0.4f), rect);
        }

        [Test]
        public void TryNormalize_ReturnsFalseForABoxWithNoWidthOrNoHeight_AndLeavesTheRectEmpty()
        {
            foreach (var box in new[]
                     {
                         new[] { 0.5f, 0.2f, 0.5f, 0.6f },
                         new[] { 0.2f, 0.5f, 0.6f, 0.5f },
                         new[] { 0f, 0f, 0f, 0f },
                         new[] { 100f, 50f, 100f, 90f }
                     })
            {
                Assert.IsFalse(BoundingBoxUtils.TryNormalize(box, 640, 480, out var rect), string.Join(", ", box));
                Assert.AreEqual(Rect.zero, rect);
            }
        }

        [Test]
        public void TryNormalize_ReturnsFalseForABoxThatLiesWhollyOutsideTheImage()
        {
            Assert.IsFalse(BoundingBoxUtils.TryNormalize(new[] { 700f, 10f, 800f, 20f }, 640, 480, out _), "right of the image");
            Assert.IsFalse(BoundingBoxUtils.TryNormalize(new[] { 10f, 500f, 20f, 600f }, 640, 480, out _), "below the image");
            Assert.IsFalse(BoundingBoxUtils.TryNormalize(new[] { -50f, -30f, -10f, -5f }, 640, 480, out _), "left of and above the image");
        }

        [Test]
        public void TryNormalize_ClampsABoxThatPartlyLeavesTheImage()
        {
            Assert.IsTrue(BoundingBoxUtils.TryNormalize(new[] { -10f, 40f, 320f, 240f }, 640, 480, out var rect));

            AssertRect(new Rect(0f, 40f / 480f, 0.5f, 0.5f - 40f / 480f), rect);
        }

        [Test]
        public void TryNormalize_ConvertsAWholeImageBoxInPixelsToTheUnitRect()
        {
            Assert.IsTrue(BoundingBoxUtils.TryNormalize(new[] { 0f, 0f, 640f, 480f }, 640, 480, out var rect));

            AssertRect(new Rect(0f, 0f, 1f, 1f), rect);
        }

        // Only the far corner (x2, y2) decides: a pixel box that ends within one pixel of the origin is read as the whole image.
        [Test]
        public void TryNormalize_Currently_ReadsAPixelBoxWhoseFarCornerIsAtMostOnePixelAsNormalized()
        {
            Assert.IsTrue(BoundingBoxUtils.TryNormalize(new[] { 0f, 0f, 1f, 1f }, 640, 480, out var rect));

            AssertRect(new Rect(0f, 0f, 1f, 1f), rect);
        }

        // A normalized box whose far corner is a hair past 1 (float error: a box that reaches the edge of the image) used to be read
        // as pixels and shrink to a speck at the top left. It is a normalized box, clamped to the image.
        [TestCase(1.0001f, 0.9f)]
        [TestCase(0.9f, 1.0001f)]
        [TestCase(1.009f, 1.009f)]
        public void TryNormalize_ReadsANormalizedBoxJustPastOneAsNormalized_AndClampsIt(float x2, float y2)
        {
            Assert.IsTrue(BoundingBoxUtils.TryNormalize(new[] { 0.5f, 0.5f, x2, y2 }, 640, 480, out var rect));

            AssertRect(new Rect(0.5f, 0.5f, Mathf.Min(x2, 1f) - 0.5f, Mathf.Min(y2, 1f) - 0.5f), rect);
        }

        // Past the tolerance it is pixels.
        [Test]
        public void TryNormalize_ReadsAFarCornerPastOnePointOhOneAsPixels()
        {
            Assert.IsTrue(BoundingBoxUtils.TryNormalize(new[] { 0f, 0f, 2f, 2f }, 640, 480, out var rect));

            AssertRect(new Rect(0f, 0f, 2f / 640f, 2f / 480f), rect);
        }

        // ---- NormalizedToPixels (no test of its own exists) ----

        [Test]
        public void NormalizedToPixels_ScalesTheOriginAndTheSizeByTheImageSize()
        {
            var pixels = BoundingBoxUtils.NormalizedToPixels(new Rect(0.1f, 0.2f, 0.4f, 0.3f), 640, 480);

            AssertRect(new Rect(64f, 96f, 256f, 144f), pixels);
        }

        [TestCase(0, 480)]
        [TestCase(640, 0)]
        [TestCase(0, 0)]
        [TestCase(-640, 480)]
        [TestCase(640, -480)]
        public void NormalizedToPixels_ReturnsAnEmptyRectForAnImageSizeNotAboveZero(int width, int height)
        {
            Assert.AreEqual(Rect.zero, BoundingBoxUtils.NormalizedToPixels(new Rect(0.1f, 0.2f, 0.4f, 0.3f), width, height));
        }

        [Test]
        public void NormalizedToPixels_DoesNotClampOrCheckTheRect()
        {
            var pixels = BoundingBoxUtils.NormalizedToPixels(new Rect(-0.5f, 0.5f, 2f, -1f), 640, 480);

            AssertRect(new Rect(-320f, 240f, 1280f, -480f), pixels);
        }

        [Test]
        public void NormalizedToPixels_UndoesTryNormalize_ForABoxInsideTheImage()
        {
            Assert.IsTrue(BoundingBoxUtils.TryNormalize(new[] { 64f, 96f, 320f, 240f }, 640, 480, out var normalized));

            var pixels = BoundingBoxUtils.NormalizedToPixels(normalized, 640, 480);

            AssertRect(new Rect(64f, 96f, 256f, 144f), pixels, "the result is x, y, width, height, not two corners");
        }

        private static void AssertRect(Rect expected, Rect actual, string message = "")
        {
            const float tolerance = 1e-4f;
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance), "x. " + message);
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance), "y. " + message);
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(tolerance), "width. " + message);
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(tolerance), "height. " + message);
        }
    }
}
