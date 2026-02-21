using System.Collections.Generic;
using Newtonsoft.Json;

namespace HouseMapper.Core
{
    public class RleMask
    {
        [JsonProperty("size")]
        public List<int> Size;

        [JsonProperty("counts")]
        public string Counts;
    }

    public class SegmentAnnotation
    {
        [JsonProperty("label")]
        public string Label;

        [JsonProperty("score")]
        public float Score;

        [JsonProperty("bbox")]
        public List<float> Bbox;

        [JsonProperty("mask")]
        public RleMask Mask;
    }

    public class SegmentResult
    {
        [JsonProperty("annotations")]
        public List<SegmentAnnotation> Annotations;

        [JsonProperty("image_size")]
        public List<int> ImageSize;
    }
}
