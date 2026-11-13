using System.Globalization;
using CompanionScan.Core;
using TMPro;
using UnityEngine;

namespace CompanionScan.UI
{
    /// <summary>
    /// In-headset settings: the CV server, the privacy policy, the open-source licences and the
    /// agreement to send camera snapshots.
    ///
    /// Opens automatically on first run (no server URL configured) and with the
    /// left controller Menu button. While it is open, <see cref="XRControllerInput"/>
    /// routes A to <see cref="EditUrl"/> (Horizon OS system keyboard), X to
    /// <see cref="EditToken"/>, B to <see cref="Apply"/>, Y to <see cref="OpenPrivacyPolicy"/>,
    /// either trigger to <see cref="ShowLicenses"/> and either grip to
    /// <see cref="WithdrawCaptureConsent"/>; capture controls are suspended.
    ///
    /// The camera notice (<see cref="CaptureConsentPanel"/>) and the licences view
    /// (<see cref="LicensesPanel"/>) are added next to this component at run time.
    ///
    /// The CV server requires a bearer token on every route. The token is typed with the
    /// system keyboard in secure mode, stored only as a PlayerPrefs override (see
    /// <see cref="ServerConfig.ResolveApiToken"/>) and never shown: the panel only says
    /// whether one is set.
    ///
    /// The URL is stored as a PlayerPrefs override (see
    /// <see cref="ServerConfig.ResolveBaseUrl"/>); the ServerConfig asset is never
    /// modified at runtime. Optional TMP input fields can be wired for a
    /// pointer-driven canvas; otherwise a world-space panel is built at runtime.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private ServerConfig config;
        [SerializeField] private TMP_InputField urlField;
        [SerializeField] private TMP_InputField boxThresholdField;
        [SerializeField] private TMP_InputField textThresholdField;

        [Header("In-headset panel")]
        [Tooltip("Open the panel at startup when no server URL is configured.")]
        [SerializeField] private bool openOnFirstRun = true;
        [SerializeField] private float panelDistance = 1.0f;
        [SerializeField] private float panelScale = 0.001f;
        [SerializeField] private Color backgroundColor = new(0.05f, 0.05f, 0.1f, 0.92f);
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color errorColor = new(1f, 0.45f, 0.4f, 1f);

        private const float PanelWidth = 700f;
        private const float PanelHeight = 490f;
        private const string Hint = "A  URL     X  API token     B  save     Menu  close";
        private const string MoreHint = "Y  privacy policy     Trigger  licences     Grip  withdraw";

        private HeadsetPanel _panel;
        private TMP_Text _urlText;
        private TMP_Text _tokenText;
        private TMP_Text _statusText;
        private TMP_Text _consentText;
        private TouchScreenKeyboard _keyboard;
        private bool _keyboardEditsToken;
        private string _draftUrl = string.Empty;
        private bool _statusIsError;
        private bool _isOpen;

        /// <summary>Most recently enabled panel (the scene normally has one).</summary>
        public static SettingsPanel Active { get; private set; }

        public bool IsOpen => _isOpen;
        public ServerConfig Config => config;
        public string DraftUrl => _draftUrl;
        public string StatusMessage { get; private set; } = string.Empty;

        /// <summary>Use a specific config asset (e.g. the pipeline's) instead of the Resources default.</summary>
        public void SetConfig(ServerConfig serverConfig)
        {
            if (serverConfig != null)
            {
                config = serverConfig;
            }
        }

        private void Awake()
        {
            if (config == null)
            {
                config = Resources.Load<ServerConfig>(ServerConfig.ResourcesPath);
            }

            // Built at run time like this panel. The notice must exist before the first capture:
            // without it the pipeline sends nothing.
            if (Application.isPlaying)
            {
                if (GetComponent<CaptureConsentPanel>() == null)
                {
                    gameObject.AddComponent<CaptureConsentPanel>();
                }

                if (GetComponent<LicensesPanel>() == null)
                {
                    gameObject.AddComponent<LicensesPanel>();
                }
            }
        }

        private void OnEnable()
        {
            Active = this;
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }

            CloseKeyboard();
        }

        private void OnDestroy()
        {
            _panel?.Destroy();
        }

        private void Start()
        {
            if (config == null)
            {
                Debug.LogWarning("[Settings] No ServerConfig found; server settings are unavailable.");
                return;
            }

            _draftUrl = config.ResolveBaseUrl();
            if (urlField != null)
            {
                urlField.text = _draftUrl;
            }

            if (boxThresholdField != null)
            {
                boxThresholdField.text = config.ResolveBoxThreshold().ToString(CultureInfo.InvariantCulture);
            }

            if (textThresholdField != null)
            {
                textThresholdField.text = config.ResolveTextThreshold().ToString(CultureInfo.InvariantCulture);
            }

            if (openOnFirstRun && !config.HasBaseUrl)
            {
                Open("Enter the CV server URL to start scanning.");
            }
        }

        private void Update()
        {
            PollKeyboard();
            if (_isOpen && _keyboard == null)
            {
                _panel?.KeepInView();
            }
        }

        public void Toggle()
        {
            if (_isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open(string message = null)
        {
            if (config == null)
            {
                return;
            }

            _draftUrl = config.ResolveBaseUrl();
            _isOpen = true;
            if (urlField == null)
            {
                EnsurePanel();
                _panel.Show();
            }

            SetStatus(message ?? "Current server shown above.", false);
        }

        public void Close()
        {
            CloseKeyboard();
            _isOpen = false;
            _panel?.Hide();
        }

        /// <summary>Y button: open the privacy policy in the headset's browser.</summary>
        public void OpenPrivacyPolicy()
        {
            PrivacyPolicy.Open();
            SetStatus("Opening the privacy policy in the browser.", false);
        }

        /// <summary>Trigger: show the open-source licences; Menu there comes back here.</summary>
        public void ShowLicenses()
        {
            var licenses = GetComponent<LicensesPanel>();
            if (licenses == null)
            {
                SetStatus("The open-source licences are not available.", true);
                return;
            }

            Close();
            licenses.Open(() => Open());
        }

        /// <summary>
        /// Grip: withdraw the agreement to send camera snapshots. Nothing is sent until the user
        /// agrees again, which the next capture asks for.
        /// </summary>
        public void WithdrawCaptureConsent()
        {
            if (!CaptureConsent.IsGranted)
            {
                SetStatus("There is no agreement to withdraw. The next capture asks first.", false);
                return;
            }

            CaptureConsent.Withdraw();
            SetStatus("Agreement withdrawn. The next capture asks again before sending anything.", false);
        }

        /// <summary>X button: enter the CV server's API token with the system keyboard (secure input).</summary>
        public void EditToken()
        {
            if (!_isOpen)
            {
                Open();
            }

            if (!TouchScreenKeyboard.isSupported)
            {
                SetStatus($"No system keyboard here. In the Editor set {ServerConfig.EditorApiTokenEnvVar}.", true);
                return;
            }

            CloseKeyboard();
            _keyboardEditsToken = true;
            _keyboard = TouchScreenKeyboard.Open(string.Empty, TouchScreenKeyboardType.Default,
                false, false, true, false, "API token");
            SetStatus("Type the server's API token, then press Done (empty removes it).", false);
        }

        /// <summary>Store <paramref name="token"/> as the API token; an empty value removes it.</summary>
        public void SaveToken(string token)
        {
            if (config == null)
            {
                return;
            }

            config.SaveApiTokenOverride(token);
            SetStatus(config.HasApiToken ? "API token saved." : "API token removed.", false);
        }

        /// <summary>A button: edit the URL with the system keyboard.</summary>
        public void EditUrl()
        {
            if (!_isOpen)
            {
                Open();
            }

            if (!TouchScreenKeyboard.isSupported)
            {
                SetStatus($"No system keyboard here. In the Editor set {ServerConfig.EditorServerUrlEnvVar}.", true);
                return;
            }

            CloseKeyboard();
            _keyboardEditsToken = false;
            _keyboard = TouchScreenKeyboard.Open(_draftUrl ?? string.Empty, TouchScreenKeyboardType.URL,
                false, false, false, false, ServerConfig.ExampleUrl);
            SetStatus("Type the server URL, then press Done.", false);
        }

        /// <summary>
        /// B button / Save: validate the URL (and optional threshold fields) and
        /// store it as the PlayerPrefs override. Closes the panel on success.
        /// </summary>
        public void Apply()
        {
            if (config == null)
            {
                return;
            }

            if (urlField != null)
            {
                _draftUrl = urlField.text;
            }

            ApplyThresholdFields();

            if (TrySaveUrl(_draftUrl, out var message))
            {
                SetStatus(message, false);
                Close();
            }
            else
            {
                SetStatus(message, true);
            }
        }

        /// <summary>
        /// Normalize <paramref name="input"/>, check it against this build's transport
        /// policy (see <see cref="ServerConfig.IsUrlAllowed"/>) and persist it as the
        /// server URL override.
        /// </summary>
        public bool TrySaveUrl(string input, out string message)
        {
            if (config == null)
            {
                message = "No ServerConfig available.";
                return false;
            }

            if (!ServerConfig.TryNormalizeBaseUrl(input, out var normalized, out var error) ||
                !config.IsUrlAllowed(normalized, out error))
            {
                message = error;
                return false;
            }

            config.SaveBaseUrlOverride(normalized);
            _draftUrl = normalized;
            message = "Saved " + normalized;
            Debug.Log("[Settings] CV server URL set to " + normalized);
            return true;
        }

        private void ApplyThresholdFields()
        {
            float? box = null;
            float? text = null;
            if (boxThresholdField != null &&
                float.TryParse(boxThresholdField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float boxValue))
            {
                box = Mathf.Clamp01(boxValue);
            }

            if (textThresholdField != null &&
                float.TryParse(textThresholdField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float textValue))
            {
                text = Mathf.Clamp01(textValue);
            }

            if (box.HasValue || text.HasValue)
            {
                config.SaveThresholdOverrides(box, text);
            }
        }

        private void PollKeyboard()
        {
            if (_keyboard == null)
            {
                return;
            }

            if (_keyboardEditsToken)
            {
                PollTokenKeyboard();
                return;
            }

            switch (_keyboard.status)
            {
                case TouchScreenKeyboard.Status.Visible:
                    if (_keyboard.text != _draftUrl)
                    {
                        _draftUrl = _keyboard.text;
                        RefreshPanel();
                    }
                    break;
                case TouchScreenKeyboard.Status.Done:
                    _draftUrl = _keyboard.text ?? string.Empty;
                    _keyboard = null;
                    Apply();
                    break;
                case TouchScreenKeyboard.Status.Canceled:
                case TouchScreenKeyboard.Status.LostFocus:
                    _keyboard = null;
                    _draftUrl = config != null ? config.ResolveBaseUrl() : string.Empty;
                    SetStatus("Edit cancelled.", false);
                    break;
            }
        }

        private void PollTokenKeyboard()
        {
            switch (_keyboard.status)
            {
                case TouchScreenKeyboard.Status.Done:
                    var token = _keyboard.text ?? string.Empty;
                    _keyboard = null;
                    _keyboardEditsToken = false;
                    SaveToken(token);
                    break;
                case TouchScreenKeyboard.Status.Canceled:
                case TouchScreenKeyboard.Status.LostFocus:
                    _keyboard = null;
                    _keyboardEditsToken = false;
                    SetStatus("Edit cancelled.", false);
                    break;
            }
        }

        private void CloseKeyboard()
        {
            if (_keyboard != null)
            {
                if (_keyboard.active)
                {
                    _keyboard.active = false;
                }

                _keyboard = null;
            }
        }

        private void SetStatus(string message, bool isError)
        {
            StatusMessage = message ?? string.Empty;
            _statusIsError = isError;
            if (isError)
            {
                Debug.LogWarning("[Settings] " + StatusMessage);
            }

            RefreshPanel();
        }

        private void RefreshPanel()
        {
            if (_urlText != null)
            {
                _urlText.text = string.IsNullOrWhiteSpace(_draftUrl)
                    ? $"<color=#9AA0B4>{ServerConfig.ExampleUrl}</color>"
                    : _draftUrl;
            }

            if (_tokenText != null)
            {
                _tokenText.text = config != null && config.HasApiToken
                    ? "API token: set"
                    : "<color=#9AA0B4>API token: not set (X)</color>";
            }

            if (_statusText != null)
            {
                _statusText.text = StatusMessage;
                _statusText.color = _statusIsError ? errorColor : textColor;
            }

            if (_consentText != null)
            {
                _consentText.text = DescribeCaptureConsent();
            }
        }

        /// <summary>The settings line about the agreement to send camera snapshots.</summary>
        public static string DescribeCaptureConsent()
        {
            if (!CaptureConsent.IsGranted)
            {
                return "Camera snapshots: the first capture asks you first";
            }

            var agreedAt = CaptureConsent.AgreedAtUtc;
            var date = agreedAt.Length >= 10 ? agreedAt.Substring(0, 10) : agreedAt;
            return string.IsNullOrEmpty(date)
                ? "Camera snapshots: you agreed (Grip withdraws)"
                : $"Camera snapshots: you agreed on {date} (Grip withdraws)";
        }

        // ── World-space panel ─────────────────────────────────────────────

        private void EnsurePanel()
        {
            if (_panel != null)
            {
                return;
            }

            _panel = new HeadsetPanel("ServerSettingsPanel", PanelWidth, PanelHeight, panelScale, panelDistance, backgroundColor);
            _panel.AddText("Title", "Settings", 40f, 52f, FontStyles.Bold, textColor);
            _urlText = _panel.AddText("Url", string.Empty, 32f, 44f, FontStyles.Normal, textColor);
            _tokenText = _panel.AddText("Token", string.Empty, 28f, 40f, FontStyles.Normal, textColor);
            _statusText = _panel.AddText("Status", string.Empty, 24f, 64f, FontStyles.Normal, textColor, wrap: true);
            _consentText = _panel.AddText("CaptureConsent", string.Empty, 24f, 34f, FontStyles.Normal, textColor);
            _panel.AddText("PrivacyPolicy", "Privacy policy: " + PrivacyPolicy.DisplayUrl, 24f, 34f, FontStyles.Normal,
                textColor, richText: false);
            _panel.AddText("Hint", Hint, 24f, 34f, FontStyles.Normal, textColor);
            _panel.AddText("MoreHint", MoreHint, 24f, 34f, FontStyles.Normal, textColor);

            RefreshPanel();
        }
    }
}
