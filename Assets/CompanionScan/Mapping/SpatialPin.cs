using System;
using UnityEngine;

namespace CompanionScan.Mapping
{
    /// <summary>
    /// Identifies which capture system produced a pin.
    /// </summary>
    public enum PinSource
    {
        /// <summary>Pin produced by the remote CV server pipeline (Florence2 / GroundingDINO / SAM2).</summary>
        CvServer,
        /// <summary>Pin produced by Meta Quest Scene Understanding (OVRAnchor / OVRRoomLayout).</summary>
        MetaScene
    }

    /// <summary>
    /// Records which raycast strategy produced the 3D placement.
    /// </summary>
    public enum RaycastHitType
    {
        ArPlane,
        MetaPlane,
        Physics,
        Fallback
    }

    [Serializable]
    public struct SerializableRect
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public SerializableRect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
    }

    /// <summary>
    /// A spatial observation produced by either the CV server pipeline or Meta Scene
    /// Understanding. Both systems write to the same <see cref="SpatialPinManager"/>
    /// using this unified type.
    /// </summary>
    [Serializable]
    public class SpatialPin
    {
        // â”€â”€ Identity â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public string label;
        public string semanticLabel;
        public float score;
        public PinSource source;

        // â”€â”€ Spatial â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public Vector3 position;
        public Vector3 normal;
        public Quaternion rotation = Quaternion.identity;

        // â”€â”€ 2D (CV-only, zeroed for MetaScene pins) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public SerializableRect bboxPixels;
        public SerializableRect bboxNormalized;

        // â”€â”€ Geometry (MetaScene-only, zeroed for CV pins) â”€â”€â”€â”€â”€â”€â”€â”€
        public Vector3 planeSize;
        public Vector3 volumeSize;
        public string anchorUuid;

        // â”€â”€ Metadata â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public string sourcePrompt;
        public double timestamp;
            public string captureId;
        public string captureSessionId;
        public int captureFrameIndex;
            public float placementConfidence;
            public RaycastHitType hitType;
        public int trackId;
    }
}

