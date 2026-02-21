using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace HouseMapper.Core
{
    public class FlorenceResult
    {
        [JsonProperty("task")]
        public string Task;

        [JsonProperty("results")]
        public JToken Results;

        [JsonProperty("text")]
        public string Text;
    }
}
