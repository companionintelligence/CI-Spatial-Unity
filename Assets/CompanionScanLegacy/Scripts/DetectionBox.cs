using UnityEngine;

namespace PresentFutures.XRAI
{
    public class DetectionBox
    {
        // Normalized (0-1) bounding box relative to the source image.
        public Rect BoundingBox;
        public string Label;
    }
}
