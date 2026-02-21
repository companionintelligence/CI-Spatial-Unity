using Newtonsoft.Json;

namespace HouseMapper.Core
{
    public class SegmentRequest
    {
        [JsonProperty("image")]
        public string Image;

        [JsonProperty("prompt")]
        public string Prompt;

        [JsonProperty("box_threshold")]
        public float BoxThreshold;

        [JsonProperty("text_threshold")]
        public float TextThreshold;

        [JsonProperty("grounding_model")]
        public string GroundingModel = "grounding_dino";

        [JsonProperty("return_masks")]
        public bool ReturnMasks = true;

        [JsonProperty("mask_format")]
        public string MaskFormat = "rle";
    }
}
