using System.Threading.Tasks;
using HouseMapper.Core;
using HouseMapper.Rendering;
using UnityEngine;

namespace HouseMapper.Mapping
{
    public class RoomAnalyzer : MonoBehaviour
    {
        [SerializeField] private ServerConfig serverConfig;
        [SerializeField] private MonoBehaviour cameraProviderSource;
        [SerializeField] private CaptionPanel captionPanel;
        [SerializeField] private string captionTask = "<MORE_DETAILED_CAPTION>";

        private ICameraProvider _cameraProvider;
        private CVServerClient _client;
        private bool _isBusy;

        private void Awake()
        {
            _cameraProvider = cameraProviderSource as ICameraProvider;
            _client = new CVServerClient(serverConfig);
        }

        public async void AnalyzeRoom()
        {
            if (_isBusy || _cameraProvider == null || serverConfig == null)
            {
                return;
            }

            var frame = _cameraProvider.GetCurrentFrameJpeg();
            if (frame == null)
            {
                return;
            }

            _isBusy = true;
            try
            {
                var result = await _client.FlorenceAsync(frame, captionTask);
                if (captionPanel != null && result != null && result.results != null)
                {
                    captionPanel.SetCaption(result.results.ToString());
                }
            }
            finally
            {
                _isBusy = false;
            }
        }
    }
}
