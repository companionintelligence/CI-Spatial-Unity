using UnityEngine;

namespace HouseMapper.Core
{
    [CreateAssetMenu(fileName = "ServerConfig", menuName = "HouseMapper/Server Config")]
    public class ServerConfig : ScriptableObject
    {
        public string baseUrl = "http://192.168.1.100:8000";
        public float boxThreshold = 0.35f;
        public float textThreshold = 0.25f;
    }
}
