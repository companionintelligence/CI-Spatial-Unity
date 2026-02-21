using UnityEngine;

namespace HouseMapper.Core
{
    public class CVServerClientFactory : MonoBehaviour
    {
        [SerializeField] private ServerConfig serverConfig;

        public CVServerClient CreateClient()
        {
            return new CVServerClient(serverConfig);
        }
    }
}
