using System;
using CompanionScan.Core;
using TMPro;
using UnityEngine;

namespace CompanionScan.UI
{
    /// <summary>
    /// The camera notice: shown when a capture is about to send the first snapshot off the
    /// headset (<see cref="CaptureConsent.Requested"/>). It says what is sent, where and why, and
    /// links the privacy policy. While it is open, <see cref="XRControllerInput"/> routes A to
    /// <see cref="Accept"/> (records the agreement and continues the capture), B to
    /// <see cref="Decline"/> (sends nothing) and Y to <see cref="OpenPrivacyPolicy"/>.
    /// <see cref="SettingsPanel"/> adds this component at run time.
    /// </summary>
    public class CaptureConsentPanel : MonoBehaviour
    {
        private const float PanelWidth = 760f;
        private const float PanelHeight = 600f;
        private const float PanelDistance = 1.0f;
        private const float PanelScale = 0.001f;
        private const string Hint = "A  agree and capture     B  not now     Y  privacy policy";

        private static readonly Color BackgroundColor = new(0.05f, 0.05f, 0.1f, 0.92f);
        private static readonly Color TextColor = Color.white;

        private HeadsetPanel _panel;
        private TMP_Text _bodyText;
        private Action _continueCapture;

        /// <summary>Most recently enabled notice (the scene normally has one).</summary>
        public static CaptureConsentPanel Active { get; private set; }

        public bool IsOpen { get; private set; }

        /// <summary>The text of the notice last shown.</summary>
        public string NoticeText { get; private set; } = string.Empty;

        private void OnEnable()
        {
            Active = this;
            CaptureConsent.Requested += Show;
        }

        private void OnDisable()
        {
            CaptureConsent.Requested -= Show;
            if (Active == this)
            {
                Active = null;
            }
        }

        private void OnDestroy()
        {
            _panel?.Destroy();
        }

        private void Update()
        {
            if (IsOpen)
            {
                _panel?.KeepInView();
            }
        }

        /// <summary>
        /// Opens the notice for a capture that would send a snapshot to <paramref name="serverUrl"/>;
        /// <paramref name="continueCapture"/> runs if the user agrees.
        /// </summary>
        public void Show(string serverUrl, Action continueCapture)
        {
            _continueCapture = continueCapture;
            NoticeText = BuildNotice(serverUrl);
            IsOpen = true;

            if (Application.isPlaying)
            {
                EnsurePanel();
                _bodyText.text = NoticeText;
                _panel.Show();
            }

            Debug.Log("[Consent] Asking before the first camera snapshot leaves the headset.");
        }

        /// <summary>A button: record the agreement and continue the capture that asked for it.</summary>
        public void Accept()
        {
            if (!IsOpen)
            {
                return;
            }

            CaptureConsent.Grant();
            var continueCapture = _continueCapture;
            Close();
            Debug.Log("[Consent] The user agreed to send camera snapshots; agreement recorded on the headset.");
            continueCapture?.Invoke();
        }

        /// <summary>B button: close the notice. Nothing is sent, and the next capture asks again.</summary>
        public void Decline()
        {
            if (!IsOpen)
            {
                return;
            }

            Close();
            Debug.Log("[Consent] The user did not agree; no snapshot was sent.");
        }

        /// <summary>Y button: open the privacy policy in the headset's browser.</summary>
        public void OpenPrivacyPolicy()
        {
            PrivacyPolicy.Open();
        }

        /// <summary>
        /// What the notice says: what one capture sends, to which server, what happens there, and
        /// where to read more. It must stay true to the published privacy policy.
        /// </summary>
        public static string BuildNotice(string serverUrl)
        {
            var server = string.IsNullOrWhiteSpace(serverUrl) ? "the vision server set in Settings" : serverUrl.Trim();
            return
                "Each time you press A, Companion Scan takes one still image from the headset's passthrough " +
                "cameras and sends it over HTTPS, with your API token, to your vision server:\n" +
                server + "\n\n" +
                "AI models on that server describe the image and find and outline the objects in it. The image " +
                "can show people, screens and papers in the room. Companion Intelligence receives none of it, " +
                "and your exports stay on this headset.\n\n" +
                "Your answer is recorded on the headset. You can withdraw it in Settings (Menu).\n" +
                "Privacy policy: " + PrivacyPolicy.DisplayUrl;
        }

        private void Close()
        {
            IsOpen = false;
            _continueCapture = null;
            _panel?.Hide();
        }

        private void EnsurePanel()
        {
            if (_panel != null)
            {
                return;
            }

            _panel = new HeadsetPanel("CameraNoticePanel", PanelWidth, PanelHeight, PanelScale, PanelDistance, BackgroundColor);
            _panel.AddText("Title", "Before your first capture", 34f, 48f, FontStyles.Bold, TextColor);
            // The server URL is user input: show it as plain text.
            _bodyText = _panel.AddText("Notice", string.Empty, 25f, 440f, FontStyles.Normal, TextColor,
                wrap: true, richText: false);
            _panel.AddText("Hint", Hint, 24f, 34f, FontStyles.Normal, TextColor);
        }
    }
}
