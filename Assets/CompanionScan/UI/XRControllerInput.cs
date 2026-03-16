using System;
using System.Collections.Generic;
using UnityEngine;

namespace CompanionScan.UI
{
    /// <summary>
    /// Polls OVRInput every frame and dispatches actions based on physical
    /// Quest controller buttons. Replaces the old world-space UI button
    /// approach with direct hardware input.
    /// </summary>
    public class XRControllerInput : MonoBehaviour
    {
        [SerializeField] private ControllerMenuUI menuUI;
        [SerializeField] private float inputCooldownSeconds = 0.12f;

        private bool _loggedNoController;
        private readonly Dictionary<string, float> _lastActionTime = new Dictionary<string, float>();

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
                Debug.LogWarning("[XRControllerInput] ControllerMenuUI not found in scene - will retry in Update.");
        }

        private void Update()
        {
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

            if (OVRInput.GetDown(OVRInput.Button.One))
                Dispatch("capture", menuUI.DoCapture);

            if (OVRInput.GetDown(OVRInput.Button.Two))
                Dispatch("cycle_model", menuUI.CycleModel);

            if (OVRInput.GetDown(OVRInput.Button.Three))
                Dispatch("cycle_preview", menuUI.CyclePreviewDistance);

            if (OVRInput.GetDown(OVRInput.Button.Four))
                Dispatch("save_export", menuUI.DoSaveExport);

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) ||
                OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
                Dispatch("cycle_display", menuUI.CycleDisplayMode);

            if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger) ||
                OVRInput.GetDown(OVRInput.Button.SecondaryHandTrigger))
                Dispatch("preview_gltf", menuUI.PreviewGltf);
        }

        public bool CanDispatch(string actionKey, float now)
        {
            if (inputCooldownSeconds <= 0f)
            {
                _lastActionTime[actionKey] = now;
                return true;
            }

            if (_lastActionTime.TryGetValue(actionKey, out float previous) && now - previous < inputCooldownSeconds)
            {
                return false;
            }

            _lastActionTime[actionKey] = now;
            return true;
        }

        private void Dispatch(string actionKey, Action action)
        {
            if (action == null)
            {
                return;
            }

            if (!CanDispatch(actionKey, Time.unscaledTime))
            {
                return;
            }

            action.Invoke();
        }
    }
}
