using System;
using System.Collections.Generic;

namespace HouseMapper.Core
{
    [Serializable]
    public class DetectionResult
    {
        public List<float[]> bboxes = new List<float[]>();
        public List<string> labels = new List<string>();
        public List<float> scores = new List<float>();
    }
}
