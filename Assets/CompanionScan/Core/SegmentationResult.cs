using System;
using System.Collections.Generic;

namespace CompanionScan.Core
{
    [Serializable]
    public class RleMask
    {
        public List<int> size = new List<int>();
        public string counts;
    }

    [Serializable]
    public class SegmentAnnotation
    {
        public string label;
        public float score;
        public float[] bbox;
        public RleMask mask;
    }

    [Serializable]
    public class SegmentationResult
    {
        public List<SegmentAnnotation> annotations = new List<SegmentAnnotation>();
        public List<int> image_size = new List<int>();
    }
}

