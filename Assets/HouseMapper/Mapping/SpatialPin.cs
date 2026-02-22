using System;
using UnityEngine;

namespace HouseMapper.Mapping
{
    [Serializable]
    public struct SerializableRect
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public SerializableRect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
    }

    [Serializable]
    public class SpatialPin
    {
        public string label;
        public string semanticLabel;
        public float score;
        public Vector3 position;
        public Vector3 normal;
        public SerializableRect bboxPixels;
        public SerializableRect bboxNormalized;
        public string sourcePrompt;
        public double timestamp;
    }
}
