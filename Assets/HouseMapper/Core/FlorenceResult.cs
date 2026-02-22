using System;
using Newtonsoft.Json.Linq;

namespace HouseMapper.Core
{
    [Serializable]
    public class FlorenceResult
    {
        public string task;
        public JToken results;
        public string text;
    }
}
