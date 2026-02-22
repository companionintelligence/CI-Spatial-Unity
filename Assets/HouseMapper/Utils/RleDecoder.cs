using System.Collections.Generic;
using HouseMapper.Core;
using UnityEngine;

namespace HouseMapper.Utils
{
    public static class RleDecoder
    {
        public static Texture2D DecodeToTexture(RleMask mask, Color color)
        {
            if (mask == null || mask.size.Count < 2)
            {
                return null;
            }

            int height = mask.size[0];
            int width = mask.size[1];
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];

            var counts = ParseCounts(mask.counts);
            int index = 0;
            byte value = 0;
            var fillColor = (Color32)color;
            var emptyColor = new Color32(0, 0, 0, 0);

            foreach (var run in counts)
            {
                for (int i = 0; i < run && index < pixels.Length; i++)
                {
                    int col = index / height;
                    int row = index % height;
                    int pixelIndex = (row * width) + col;
                    pixels[pixelIndex] = value == 1 ? fillColor : emptyColor;
                    index++;
                }
                value = (byte)(1 - value);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static List<int> ParseCounts(string counts)
        {
            var output = new List<int>();
            if (string.IsNullOrEmpty(counts))
            {
                return output;
            }

            var parts = counts.Split(' ');
            foreach (var part in parts)
            {
                if (int.TryParse(part, out int value))
                {
                    output.Add(value);
                }
            }

            return output;
        }
    }
}
