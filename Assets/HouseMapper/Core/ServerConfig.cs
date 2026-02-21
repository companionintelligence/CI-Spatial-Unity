using UnityEngine;

namespace HouseMapper.Core
{
    [CreateAssetMenu(menuName = "HouseMapper/Server Config", fileName = "ServerConfig")]
    public class ServerConfig : ScriptableObject
    {
        public string BaseUrl = "http://192.168.1.100:8000";
        public float DefaultBoxThreshold = 0.35f;
        public float DefaultTextThreshold = 0.25f;
        public int RequestTimeoutSeconds = 30;
    }
}
