using System.Globalization;
using CompanionScan.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CompanionScan.UI
{
    /// <summary>
    /// In-headset CV server settings.
    ///
    /// Opens automatically on first run (no server URL configured) and with the
    /// left controller Menu button. While it is open, <see cref="XRControllerInput"/>
    /// routes A to <see cref="EditUrl"/> (Horizon OS system keyboard) and B to
    /// <see cref="Apply"/>; capture controls are suspended.
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

        private const float PanelWidth = 640f;
        private const float PanelHeight = 300f;
        private const float RecenterAngleDegrees = 35f;
        private const string Hint = "A  edit URL      B  save      Menu  close";

        private GameObject _panelRoot;
        private TMP_Text _urlText;
        private TMP_Text _statusText;
        private TouchScreenKeyboard _keyboard;
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
            if (_panelRoot != null)
            {
                Destroy(_panelRoot);
            }
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
            KeepPanelInView();
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
                PlaceInFrontOfViewer();
                _panelRoot.SetActive(true);
            }

            SetStatus(message ?? "Current server shown above.", false);
        }

        public void Close()
        {
            CloseKeyboard();
            _isOpen = false;
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(false);
            }
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

            if (_statusText != null)
            {
                _statusText.text = StatusMessage;
                _statusText.color = _statusIsError ? errorColor : textColor;
            }
        }

        // ── World-space panel ─────────────────────────────────────────────

        private void EnsurePanel()
        {
            if (_panelRoot != null)
            {
                return;
            }

            // Scene root, not a child of this component: the panel is placed in
            // world space in front of the viewer and must not follow its host.
            _panelRoot = new GameObject("ServerSettingsPanel");
            _panelRoot.transform.localScale = Vector3.one * panelScale;

            var canvas = _panelRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            _panelRoot.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            _panelRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelWidth, PanelHeight);

            var background = new GameObject("Background", typeof(RectTransform));
            background.transform.SetParent(_panelRoot.transform, false);
            Stretch(background.GetComponent<RectTransform>(), 0f);
            background.AddComponent<Image>().color = backgroundColor;

            var layout = new GameObject("Layout", typeof(RectTransform));
            layout.transform.SetParent(background.transform, false);
            Stretch(layout.GetComponent<RectTransform>(), 24f);
            var group = layout.AddComponent<VerticalLayoutGroup>();
            group.spacing = 14f;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childControlWidth = true;
            group.childControlHeight = false;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;

            MakeText(layout.transform, "Title", "CV server", 40f, 52f, FontStyles.Bold);
            _urlText = MakeText(layout.transform, "Url", string.Empty, 32f, 44f, FontStyles.Normal);
            _statusText = MakeText(layout.transform, "Status", string.Empty, 24f, 64f, FontStyles.Normal);
            _statusText.textWrappingMode = TextWrappingModes.Normal;
            MakeText(layout.transform, "Hint", Hint, 24f, 34f, FontStyles.Normal);

            _panelRoot.SetActive(false);
            RefreshPanel();
        }

        /// <summary>
        /// Re-centers the open panel when it drifts out of view, e.g. when it was
        /// opened before head tracking settled or the user turned away.
        /// </summary>
        private void KeepPanelInView()
        {
            if (!_isOpen || _panelRoot == null || !_panelRoot.activeSelf || _keyboard != null)
            {
                return;
            }

            var viewer = Camera.main != null ? Camera.main.transform : null;
            if (viewer == null)
            {
                return;
            }

            // Compare headings only, so looking down at the controllers does not move the panel.
            var flatForward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 1e-4f)
            {
                return;
            }

            var toPanel = _panelRoot.transform.position - viewer.position;
            var flatToPanel = Vector3.ProjectOnPlane(toPanel, Vector3.up);
            bool misplaced = toPanel.magnitude > panelDistance * 2f || Mathf.Abs(toPanel.y) > panelDistance * 0.5f;
            bool outOfView = flatToPanel.sqrMagnitude > 1e-4f &&
                             Vector3.Angle(flatForward, flatToPanel) > RecenterAngleDegrees;
            if (misplaced || outOfView)
            {
                PlaceInFrontOfViewer();
            }
        }

        private void PlaceInFrontOfViewer()
        {
            var viewer = Camera.main != null ? Camera.main.transform : null;
            if (viewer == null || _panelRoot == null)
            {
                return;
            }

            var forward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = viewer.forward;
            }

            forward.Normalize();
            _panelRoot.transform.SetPositionAndRotation(
                viewer.position + forward * panelDistance,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        private TMP_Text MakeText(Transform parent, string name, string text, float size, float height, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelWidth - 48f, height);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
