using HouseMapper.Core;
using HouseMapper.Utils;
using NUnit.Framework;
using UnityEngine;

namespace HouseMapper.Tests.Editor
{
    public class RleDecoderTests
    {
        [Test]
        public void DecodeToTexture_SetsExpectedPixels()
        {
            var mask = new RleMask
            {
                size = new System.Collections.Generic.List<int> { 2, 2 },
                counts = "0 1 3"
            };

            var color = new Color(1f, 0f, 0f, 1f);
            var texture = RleDecoder.DecodeToTexture(mask, color);

            Assert.IsNotNull(texture);
            var pixel00 = texture.GetPixel(0, 0);
            var pixel10 = texture.GetPixel(1, 0);
            var pixel01 = texture.GetPixel(0, 1);
            var pixel11 = texture.GetPixel(1, 1);

            Assert.Greater(pixel00.a, 0.9f);
            Assert.Less(pixel10.a, 0.1f);
            Assert.Less(pixel01.a, 0.1f);
            Assert.Less(pixel11.a, 0.1f);
        }
    }
}
