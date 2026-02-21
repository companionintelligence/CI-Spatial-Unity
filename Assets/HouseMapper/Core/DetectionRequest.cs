using Newtonsoft.Json;

namespace HouseMapper.Core
{
    public class DetectionRequest
    {
        [JsonProperty("image")]
        public string Image;

        [JsonProperty("prompt")]
        public string Prompt;

        [JsonProperty("box_threshold")]
        public float BoxThreshold;

        [JsonProperty("text_threshold")]
        public float TextThreshold;
    }
}
