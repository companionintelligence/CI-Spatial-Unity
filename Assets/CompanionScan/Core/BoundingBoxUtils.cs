using System.Collections.Generic;
using UnityEngine;

namespace CompanionScan.Core
{
    public static class BoundingBoxUtils
    {
        public static bool TryNormalize(float[] bbox, int imageWidth, int imageHeight, out Rect normalized)
        {
            var result = Validate(
                bbox,
                imageWidth,
                imageHeight,
                BoundingBoxValidationOptions.Default,
                -1);

            normalized = result.NormalizedRect;
            return result.IsValid;
        }

        public static BoundingBoxValidationResult Validate(
            float[] bbox,
            int imageWidth,
            int imageHeight,
            BoundingBoxValidationOptions options,
            int index)
        {
            var result = new BoundingBoxValidationResult
            {
                Index = index,
                IsValid = false,
                InputWasNormalized = false,
                NormalizedRect = Rect.zero,
                PixelRect = Rect.zero,
                RejectReason = BoundingBoxRejectReason.None,
                DebugMessage = string.Empty
            };

            if (bbox == null || bbox.Length < 4)
            {
                result.RejectReason = BoundingBoxRejectReason.NullOrTooShort;
                result.DebugMessage = "bbox is null or has fewer than 4 values";
                return result;
            }

            float x1 = bbox[0];
            float y1 = bbox[1];
            float x2 = bbox[2];
            float y2 = bbox[3];

            bool alreadyNormalized = x2 <= 1f && y2 <= 1f;
            result.InputWasNormalized = alreadyNormalized;

            if (!alreadyNormalized)
            {
                if (imageWidth <= 0 || imageHeight <= 0)
                {
                    result.RejectReason = BoundingBoxRejectReason.InvalidImageDimensions;
                    result.DebugMessage =
                        $"image dimensions must be > 0 for pixel bbox ({imageWidth}x{imageHeight})";
                    return result;
                }

                x1 /= imageWidth;
                x2 /= imageWidth;
                y1 /= imageHeight;
                y2 /= imageHeight;
            }

            float minX = Mathf.Clamp01(Mathf.Min(x1, x2));
            float maxX = Mathf.Clamp01(Mathf.Max(x1, x2));
            float minY = Mathf.Clamp01(Mathf.Min(y1, y2));
            float maxY = Mathf.Clamp01(Mathf.Max(y1, y2));

            float width = maxX - minX;
            float height = maxY - minY;
            if (width <= 0f || height <= 0f)
            {
                result.RejectReason = BoundingBoxRejectReason.DegenerateAfterClamp;
                result.DebugMessage =
                    $"bbox collapsed after clamp (w={width:0.######}, h={height:0.######})";
                return result;
            }

            var normalizedRect = new Rect(minX, minY, width, height);
            var pixelRect = NormalizedToPixels(normalizedRect, imageWidth, imageHeight);

            float minPixelWidth = Mathf.Max(0f, options.MinPixelWidth);
            float minPixelHeight = Mathf.Max(0f, options.MinPixelHeight);
            if (pixelRect.width < minPixelWidth || pixelRect.height < minPixelHeight)
            {
                result.RejectReason = BoundingBoxRejectReason.BelowMinPixelSize;
                result.DebugMessage =
                    $"bbox too small ({pixelRect.width:0.######}x{pixelRect.height:0.######} px), min={minPixelWidth:0.######}x{minPixelHeight:0.######}";
                return result;
            }

            result.IsValid = true;
            result.NormalizedRect = normalizedRect;
            result.PixelRect = pixelRect;
            return result;
        }

        public static List<BoundingBoxValidationResult> ValidateAll(
            DetectionResult detection,
            int imageWidth,
            int imageHeight,
            BoundingBoxValidationOptions options)
        {
            var results = new List<BoundingBoxValidationResult>();
            if (detection?.bboxes == null)
            {
                return results;
            }

            for (int i = 0; i < detection.bboxes.Count; i++)
            {
                results.Add(Validate(detection.bboxes[i], imageWidth, imageHeight, options, i));
            }

            return results;
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