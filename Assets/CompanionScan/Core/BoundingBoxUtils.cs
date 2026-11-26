using UnityEngine;

namespace CompanionScan.Core
{
    public static class BoundingBoxUtils
    {
        /// <summary>
        /// A box whose far corner (x2 and y2) is at most this is read as fractions of the image, otherwise as
        /// pixels. The CV server sends fractions; a bit over 1 is float error, not a one-pixel box.
        /// </summary>
        public const float NormalizedLimit = 1.01f;

        public static bool TryNormalize(float[] bbox, int imageWidth, int imageHeight, out Rect normalized)
        {
            normalized = Rect.zero;
            if (bbox == null || bbox.Length < 4)
            {
                return false;
            }

            float x1 = bbox[0];
            float y1 = bbox[1];
            float x2 = bbox[2];
            float y2 = bbox[3];

            // A far corner a hair past 1 is a normalized box with float error (a box that reaches the edge of
            // the image), not a pixel box one pixel wide; it is clamped to 1 below.
            bool alreadyNormalized = x2 <= NormalizedLimit && y2 <= NormalizedLimit;
            if (!alreadyNormalized)
            {
                if (imageWidth <= 0 || imageHeight <= 0)
                {
                    return false;
                }

                x1 /= imageWidth;
                x2 /= imageWidth;
                y1 /= imageHeight;
                y2 /= imageHeight;
            }

            float minX = Mathf.Min(x1, x2);
            float maxX = Mathf.Max(x1, x2);
            float minY = Mathf.Min(y1, y2);
            float maxY = Mathf.Max(y1, y2);

            minX = Mathf.Clamp01(minX);
            maxX = Mathf.Clamp01(maxX);
            minY = Mathf.Clamp01(minY);
            maxY = Mathf.Clamp01(maxY);

            float width = maxX - minX;
            float height = maxY - minY;
            if (width <= 0f || height <= 0f)
            {
                return false;
            }

            normalized = new Rect(minX, minY, width, height);
            return true;
        }

        public static Rect NormalizedToPixels(Rect normalized, int imageWidth, int imageHeight)
        {
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return Rect.zero;
            }

            float x = normalized.x * imageWidth;
            float y = normalized.y * imageHeight;
            float w = normalized.width * imageWidth;
            float h = normalized.height * imageHeight;
            return new Rect(x, y, w, h);
        }
    }
}

