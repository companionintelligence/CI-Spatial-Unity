using HouseMapper.Core;
using NUnit.Framework;
using UnityEngine;

namespace HouseMapper.Tests
{
    public class BoundingBoxUtilsTests
    {
        [Test]
        public void TryNormalize_AlreadyNormalized_ReturnsSameRect()
        {
            float[] bbox = { 0.1f, 0.2f, 0.4f, 0.6f };

            bool ok = BoundingBoxUtils.TryNormalize(bbox, 640, 480, out Rect rect);

            Assert.IsTrue(ok);
            Assert.That(rect.x, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(rect.y, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(rect.width, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(rect.height, Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void TryNormalize_PixelInput_ConvertsToNormalized()
        {
            float[] bbox = { 64f, 96f, 320f, 240f };

            bool ok = BoundingBoxUtils.TryNormalize(bbox, 640, 480, out Rect rect);

            Assert.IsTrue(ok);
            Assert.That(rect.x, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(rect.y, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(rect.width, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(rect.height, Is.EqualTo(0.3f).Within(0.0001f));
        }

        [Test]
        public void TryNormalize_ReversedCoords_ProducesPositiveRect()
        {
            float[] bbox = { 0.8f, 0.9f, 0.2f, 0.1f };

            bool ok = BoundingBoxUtils.TryNormalize(bbox, 640, 480, out Rect rect);

            Assert.IsTrue(ok);
            Assert.That(rect.x, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(rect.y, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(rect.width, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(rect.height, Is.EqualTo(0.8f).Within(0.0001f));
        }

        [Test]
        public void TryNormalize_ClampsOutOfRangeValues()
        {
            float[] bbox = { -10f, -20f, 1000f, 2000f };

            bool ok = BoundingBoxUtils.TryNormalize(bbox, 100, 100, out Rect rect);

            Assert.IsTrue(ok);
            Assert.That(rect.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(rect.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(rect.width, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(rect.height, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void TryNormalize_InvalidDimensions_ReturnsFalse()
        {
            float[] bbox = { 10f, 10f, 20f, 20f };

            bool ok = BoundingBoxUtils.TryNormalize(bbox, 0, 0, out Rect rect);

            Assert.IsFalse(ok);
            Assert.AreEqual(Rect.zero, rect);
        }
    }
}
