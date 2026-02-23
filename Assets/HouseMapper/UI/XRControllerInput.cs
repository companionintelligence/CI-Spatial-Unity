using UnityEngine;

namespace HouseMapper.UI
{
    /// <summary>
    /// Polls OVRInput every frame and dispatches actions based on physical
    /// Quest controller buttons.  Replaces the old world-space UI button
    /// approach with direct hardware input.
    ///
    /// Button mapping (right controller):
    ///   A  (Button.One)   → Capture snapshot
    ///   B  (Button.Two)   → Cycle active model (Florence → GDino → SAM2 → All)
    /// Left controller:
    ///   X  (Button.Three) → Cycle preview distance (Near → Far → Off)
    ///   Y  (Button.Four)  → Save &amp; Export GLTF
    /// Either trigger:
    ///   Primary / Secondary Index Trigger → Cycle display mode
    /// </summary>
    public class XRControllerInput : MonoBehaviour
    {
        [SerializeField] private ControllerMenuUI menuUI;

        /// <summary>
        /// If null, <see cref="menuUI"/> is found at Awake via
        /// <see cref="Object.FindAnyObjectByType{T}"/>.
        /// </summary>
        private void Awake()
        {
            if (menuUI == null)
                menuUI = FindAnyObjectByType<ControllerMenuUI>();

            if (menuUI == null)
                Debug.LogWarning("[XRControllerInput] ControllerMenuUI not found in scene.");
        }

        private void Update()
        {
            if (menuUI == null) return;

            // A – Capture
            if (OVRInput.GetDown(OVRInput.Button.One))
                menuUI.DoCapture();

            // B – Cycle model
            if (OVRInput.GetDown(OVRInput.Button.Two))
                menuUI.CycleModel();

            // X – Cycle preview distance
            if (OVRInput.GetDown(OVRInput.Button.Three))
                menuUI.CyclePreviewDistance();

            // Y – Save & Export
            if (OVRInput.GetDown(OVRInput.Button.Four))
                menuUI.DoSaveExport();

            // Either trigger – Cycle display mode
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) ||
                OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
                menuUI.CycleDisplayMode();
        }
    }
}
