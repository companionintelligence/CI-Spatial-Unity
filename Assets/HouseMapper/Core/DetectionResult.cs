using System.Collections.Generic;
using Newtonsoft.Json;

namespace HouseMapper.Core
{
    public class DetectionResult
    {
        [JsonProperty("bboxes")]
        public List<List<float>> Bboxes;

        [JsonProperty("labels")]
        public List<string> Labels;

        [JsonProperty("scores")]
        public List<float> Scores;
    }
}
