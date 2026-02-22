using System.Globalization;
using HouseMapper.Core;
using TMPro;
using UnityEngine;

namespace HouseMapper.UI
{
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private ServerConfig config;
        [SerializeField] private TMP_InputField urlField;
        [SerializeField] private TMP_InputField boxThresholdField;
        [SerializeField] private TMP_InputField textThresholdField;

        private void Start()
        {
            if (config == null || urlField == null)
            {
                return;
            }

            var resolvedUrl = config.ResolveBaseUrl();
            if (!string.IsNullOrWhiteSpace(resolvedUrl))
            {
                urlField.text = resolvedUrl;
            }
        }

        public void Apply()
        {
            if (config == null)
            {
                return;
            }

            if (urlField != null)
            {
                config.baseUrl = urlField.text;
                PlayerPrefs.SetString(ServerConfig.PlayerPrefsBaseUrlKey, config.baseUrl);
                PlayerPrefs.Save();
            }

            if (boxThresholdField != null && float.TryParse(boxThresholdField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float boxValue))
            {
                config.boxThreshold = boxValue;
            }

            if (textThresholdField != null && float.TryParse(textThresholdField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float textValue))
            {
                config.textThreshold = textValue;
            }
        }
    }
}
