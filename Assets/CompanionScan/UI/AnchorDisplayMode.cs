namespace CompanionScan.UI
{
    /// <summary>
    /// Controls how CV detections are visualised on the Quest.
    /// Mirrors the original GroundingDinoController / Florence2Controller concept.
    /// </summary>
    public enum AnchorDisplayMode
    {
        /// <summary>2D bounding-box overlays on the passthrough camera feed.</summary>
        BoundingBox2D,

        /// <summary>3D spatial labels placed at raycasted world positions.</summary>
        SpatialLabel3D,

        /// <summary>Both 2D overlays and 3D labels simultaneously.</summary>
        Both
    }
}

