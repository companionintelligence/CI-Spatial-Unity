using UnityEngine;

namespace CompanionScan.UI
{
    /// <summary>
    /// Polls OVRInput every frame and dispatches actions based on physical
    /// Quest controller buttons.  Replaces the old world-space UI button
    /// approach with direct hardware input.
    ///
    /// Button mapping (right controller):
    ///   A  (Button.One)   -> Capture snapshot
    ///   B  (Button.Two)   -> Cycle active model (Florence -> GDino -> SAM2)
    /// Left controller:
    ///   X  (Button.Three) -> Cycle preview distance (Near -> Far -> Off)
    ///   Y  (Button.Four)  -> Save & Export GLTF
    /// Either index trigger:
    ///   Primary / Secondary Index Trigger -> Cycle display mode
    /// Either hand trigger:
    ///   Primary / Secondary Hand Trigger -> Preview glTF
    /// Left Menu button (Button.Start) -> open / close the settings.
    ///   While the settings panel is open, A edits the server URL, X the server's
    ///   API token, B saves, Y opens the privacy policy, either trigger shows the
    ///   open-source licences and either grip withdraws the agreement to send camera
    ///   snapshots; the capture controls above are suspended.
    /// While the open-source licences are shown, A / X turn the page, B / Y change the
    ///   document and Menu goes back to the settings.
    /// While the camera notice is shown (before the first snapshot leaves the headset),
    ///   A agrees and captures, B sends nothing and Y opens the privacy policy.
    /// </summary>
    public class XRControllerInput : MonoBehaviour
    {
        [SerializeField] private ControllerMenuUI menuUI;
        [Tooltip("Server settings panel. If null, the active SettingsPanel in the scene is used.")]
        [SerializeField] private SettingsPanel settingsPanel;
        private bool _loggedNoController;

        /// <summary>
        /// If null, <see cref="menuUI"/> is found at Awake via
        /// <see cref="Object.FindAnyObjectByType{T}"/>.
        /// </summary>
        private void Awake()
        {
            if (FindAnyObjectByType<OVRManager>() == null)
            {
                var managerGo = new GameObject("OVRManager");
                managerGo.AddComponent<OVRManager>();
            }

            if (menuUI == null)
                menuUI = FindAnyObjectByType<ControllerMenuUI>();

            if (menuUI == null)
                Debug.LogWarning("[XRControllerInput] ControllerMenuUI not found in scene â€” will retry in Update.");
        }

        private void Update()
        {
            if (HandleLicensesInput())
                return;

            if (HandleSettingsInput())
                return;

            if (HandleConsentInput())
                return;

            // Late-bind: ControllerMenuBuilder creates MenuUI in Start(),
            // so it may not exist during our Awake().
            if (menuUI == null)
            {
                menuUI = FindAnyObjectByType<ControllerMenuUI>();
                if (menuUI == null) return;
                Debug.Log("[XRControllerInput] Late-bound ControllerMenuUI.");
            }

            if (!_loggedNoController && OVRInput.GetActiveController() == OVRInput.Controller.None)
            {
                _loggedNoController = true;
                Debug.LogWarning("[XRControllerInput] No active OVR controller detected.");
            }

            // A â€“ Capture
            if (OVRInput.GetDown(OVRInput.Button.One))
                menuUI.DoCapture();

            // B â€“ Cycle model
            if (OVRInput.GetDown(OVRInput.Button.Two))
                menuUI.CycleModel();

            // X â€“ Cycle preview distance
            if (OVRInput.GetDown(OVRInput.Button.Three))
                menuUI.CyclePreviewDistance();

            // Y â€“ Save & Export
            if (OVRInput.GetDown(OVRInput.Button.Four))
                menuUI.DoSaveExport();

            // Either trigger â€“ Cycle display mode
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) ||
                OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
                menuUI.CycleDisplayMode();

            // Either hand trigger â€“ Preview glTF
            if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger) ||
                OVRInput.GetDown(OVRInput.Button.SecondaryHandTrigger))
                menuUI.PreviewGltf();
        }

        /// <summary>
        /// Menu toggles the settings panel; while it is open, A edits the URL, X the API token,
        /// B saves, Y opens the privacy policy, a trigger shows the licences and a grip withdraws
        /// the agreement to send snapshots. Returns true when the panel consumed this frame's input.
        /// </summary>
        private bool HandleSettingsInput()
        {
            if (settingsPanel == null)
                settingsPanel = SettingsPanel.Active;
            if (settingsPanel == null)
                return false;

            if (OVRInput.GetDown(OVRInput.Button.Start))
                settingsPanel.Toggle();

            if (!settingsPanel.IsOpen)
                return false;

            if (OVRInput.GetDown(OVRInput.Button.One))
                settingsPanel.EditUrl();

            if (OVRInput.GetDown(OVRInput.Button.Three))
                settingsPanel.EditToken();

            if (OVRInput.GetDown(OVRInput.Button.Two))
                settingsPanel.Apply();

            if (OVRInput.GetDown(OVRInput.Button.Four))
                settingsPanel.OpenPrivacyPolicy();

            if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger) ||
                OVRInput.GetDown(OVRInput.Button.SecondaryHandTrigger))
                settingsPanel.WithdrawCaptureConsent();

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) ||
                OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
                settingsPanel.ShowLicenses();

            return true;
        }

        /// <summary>
        /// While the licences are shown: A / X turn the page, B / Y change the document, Menu goes back.
        /// Returns true when the view consumed this frame's input.
        /// </summary>
        private static bool HandleLicensesInput()
        {
            var licenses = LicensesPanel.Active;
            if (licenses == null || !licenses.IsOpen)
                return false;

            if (OVRInput.GetDown(OVRInput.Button.Start))
            {
                licenses.Close();
                return true;
            }

            if (OVRInput.GetDown(OVRInput.Button.One))
                licenses.NextPage();

            if (OVRInput.GetDown(OVRInput.Button.Three))
                licenses.PreviousPage();

            if (OVRInput.GetDown(OVRInput.Button.Two))
                licenses.NextDocument();

            if (OVRInput.GetDown(OVRInput.Button.Four))
                licenses.PreviousDocument();

            return true;
        }

        /// <summary>
        /// While the camera notice is shown: A agrees (and the capture goes ahead), B sends nothing,
        /// Y opens the privacy policy. Returns true when the notice consumed this frame's input.
        /// </summary>
        private static bool HandleConsentInput()
        {
            var notice = CaptureConsentPanel.Active;
            if (notice == null || !notice.IsOpen)
                return false;

            if (OVRInput.GetDown(OVRInput.Button.One))
                notice.Accept();
            else if (OVRInput.GetDown(OVRInput.Button.Two))
                notice.Decline();

            if (OVRInput.GetDown(OVRInput.Button.Four))
                notice.OpenPrivacyPolicy();

            return true;
        }
    }
}

