using Newtonsoft.Json;

namespace HouseMapper.Core
{
    public class FlorenceRequest
    {
        [JsonProperty("image")]
        public string Image;

        [JsonProperty("task")]
        public string Task;

        [JsonProperty("text_input")]
        public string TextInput;
    }
}
