using UnityEngine;

namespace CompanionScan.UI
{
    /// <summary>
    /// Reads an input backend every frame and dispatches actions to the menu and the panels. The
    /// backend (<see cref="HasActiveController"/> and <see cref="GetDown"/>) is the subclass's: Quest's
    /// is <c>XRControllerInput</c>, which polls OVRInput.
    ///
    /// Button mapping (right controller):
    ///   A -> Capture snapshot
    ///   B -> Cycle active model (GDino -> SAM2 -> Florence; starts on GDino)
    /// Left controller:
    ///   X -> Cycle preview distance (Near -> Far -> Off)
    ///   Y -> Save and export glTF
    /// Either index trigger -> Cycle display mode
    /// Either grip -> Preview glTF
    /// Left Menu button -> open / close the settings.
    ///   While the settings panel is open, A edits the server URL, X the server's
    ///   API token, B saves, Y opens the privacy policy, either trigger shows the
    ///   open-source licences and either grip withdraws the agreement to send camera
    ///   snapshots; the capture controls above are suspended.
    /// While the open-source licences are shown, A / X turn the page, B / Y change the
    ///   document and Menu goes back to the settings.
    /// While the camera notice is shown (before snapshots first go to a server), A agrees and
    ///   captures, B sends nothing and Y opens the privacy policy. Menu opens the settings, which
    ///   close the notice without agreeing (both panels open at the same spot).
    /// </summary>
    public abstract class ControllerInput : MonoBehaviour, IInputSource
    {
        // Protected, not private: ControllerMenuBuilder sets "menuUI" by reflection on the subclass,
        // and reflection finds a base class's private fields only through the base class.
        [SerializeField] protected ControllerMenuUI menuUI;
        [Tooltip("Server settings panel. If null, the active SettingsPanel in the scene is used.")]
        [SerializeField] protected SettingsPanel settingsPanel;
        private bool _loggedNoController;

        /// <inheritdoc />
        public abstract bool HasActiveController { get; }

        /// <inheritdoc />
        public abstract bool GetDown(ControllerButton button);

        /// <summary>
        /// If <c>menuUI</c> is null, it is found at Awake via
        /// <see cref="Object.FindAnyObjectByType{T}"/>.
        /// </summary>
        protected virtual void Awake()
        {
            if (menuUI == null)
                menuUI = FindAnyObjectByType<ControllerMenuUI>();

            if (menuUI == null)
                Debug.LogWarning("[ControllerInput] ControllerMenuUI not found in scene — will retry in Update.");
        }

        /// <summary>
        /// True (the default) to read the input every frame. A backend that is driven by hand, such as a
        /// scripted one in a test, returns false and calls <see cref="Tick"/> itself.
        /// </summary>
        protected virtual bool TicksEveryFrame => true;

        private void Update()
        {
            if (TicksEveryFrame)
            {
                Tick();
            }
        }

        /// <summary>One frame of input: what <c>Update</c> does, callable from a test.</summary>
        public void Tick()
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
                Debug.Log("[ControllerInput] Late-bound ControllerMenuUI.");
            }

            if (!_loggedNoController && !HasActiveController)
            {
                _loggedNoController = true;
                Debug.LogWarning("[ControllerInput] No active controller detected.");
            }

            // A – Capture
            if (GetDown(ControllerButton.A))
                menuUI.DoCapture();

            // B – Cycle model
            if (GetDown(ControllerButton.B))
                menuUI.CycleModel();

            // X – Cycle preview distance
            if (GetDown(ControllerButton.X))
                menuUI.CyclePreviewDistance();

            // Y – Save & Export
            if (GetDown(ControllerButton.Y))
                menuUI.DoSaveExport();

            // Either trigger – Cycle display mode
            if (GetDown(ControllerButton.Trigger))
                menuUI.CycleDisplayMode();

            // Either grip – Preview glTF
            if (GetDown(ControllerButton.Grip))
                menuUI.PreviewGltf();
        }

        /// <summary>
        /// Menu toggles the settings panel (opening it closes the camera notice); while it is open,
        /// A edits the URL, X the API token, B saves, Y opens the privacy policy, a trigger shows the
        /// licences and a grip withdraws the agreement to send snapshots. Returns true when the panel
        /// consumed this frame's input.
        /// </summary>
        private bool HandleSettingsInput()
        {
            if (settingsPanel == null)
                settingsPanel = SettingsPanel.Active;
            if (settingsPanel == null)
                return false;

            if (GetDown(ControllerButton.Menu))
                settingsPanel.Toggle();

            if (!settingsPanel.IsOpen)
                return false;

            if (GetDown(ControllerButton.A))
                settingsPanel.EditUrl();

            if (GetDown(ControllerButton.X))
                settingsPanel.EditToken();

            if (GetDown(ControllerButton.B))
                settingsPanel.Apply();

            if (GetDown(ControllerButton.Y))
                settingsPanel.OpenPrivacyPolicy();

            if (GetDown(ControllerButton.Grip))
                settingsPanel.WithdrawCaptureConsent();

            if (GetDown(ControllerButton.Trigger))
                settingsPanel.ShowLicenses();

            return true;
        }

        /// <summary>
        /// While the licences are shown: A / X turn the page, B / Y change the document, Menu goes back.
        /// Returns true when the view consumed this frame's input.
        /// </summary>
        private bool HandleLicensesInput()
        {
            var licenses = LicensesPanel.Active;
            if (licenses == null || !licenses.IsOpen)
                return false;

            if (GetDown(ControllerButton.Menu))
            {
                licenses.Close();
                return true;
            }

            if (GetDown(ControllerButton.A))
                licenses.NextPage();

            if (GetDown(ControllerButton.X))
                licenses.PreviousPage();

            if (GetDown(ControllerButton.B))
                licenses.NextDocument();

            if (GetDown(ControllerButton.Y))
                licenses.PreviousDocument();

            return true;
        }

        /// <summary>
        /// While the camera notice is shown: A agrees (and the capture goes ahead), B sends nothing,
        /// Y opens the privacy policy. Returns true when the notice consumed this frame's input.
        /// </summary>
        private bool HandleConsentInput()
        {
            var notice = CaptureConsentPanel.Active;
            if (notice == null || !notice.IsOpen)
                return false;

            if (GetDown(ControllerButton.A))
                notice.Accept();
            else if (GetDown(ControllerButton.B))
                notice.Decline();

            if (GetDown(ControllerButton.Y))
                notice.OpenPrivacyPolicy();

            return true;
        }
    }
}
