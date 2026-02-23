using System;
using Newtonsoft.Json.Linq;

namespace CompanionScan.Core
{
    [Serializable]
    public class FlorenceResult
    {
        public string task;
        public JToken results;
        public string text;
    }
}

