using System;
using UnityEngine;

namespace CompanionScan.Core
{
    public enum BoundingBoxRejectReason
    {
        None = 0,
        NullOrTooShort = 1,
        InvalidImageDimensions = 2,
        DegenerateAfterClamp = 3,
        BelowMinPixelSize = 4
    }

    [Serializable]
    public struct BoundingBoxValidationOptions
    {
        public float MinPixelWidth;
        public float MinPixelHeight;

        public static BoundingBoxValidationOptions Default
        {
            get
            {
                return new BoundingBoxValidationOptions
                {
                    MinPixelWidth = 0f,
                    MinPixelHeight = 0f
                };
            }
        }

        public static BoundingBoxValidationOptions ForRendering(float minPixels = 1f)
        {
            float clamped = Mathf.Max(0f, minPixels);
            return new BoundingBoxValidationOptions
            {
                MinPixelWidth = clamped,
                MinPixelHeight = clamped
            };
        }
    }

    [Serializable]
    public struct BoundingBoxValidationResult
    {
        public int Index;
        public bool IsValid;
        public bool InputWasNormalized;
        public Rect NormalizedRect;
        public Rect PixelRect;
        public BoundingBoxRejectReason RejectReason;
        public string DebugMessage;
    }
}