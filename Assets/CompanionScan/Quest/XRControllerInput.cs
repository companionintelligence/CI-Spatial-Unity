using UnityEngine;

namespace CompanionScan.UI
{
    /// <summary>
    /// The Quest input backend: polls OVRInput every frame and reports the physical controller buttons.
    /// <see cref="ControllerInput"/> turns them into actions (see its button table). Replaces the old
    /// world-space UI button approach with direct hardware input.
    ///
    /// Touch controller mapping: A is Button.One, B Button.Two, X Button.Three, Y Button.Four, Menu
    /// Button.Start, a trigger is either hand's index trigger and a grip either hand's hand trigger.
    /// </summary>
    public class XRControllerInput : ControllerInput
    {
        protected override void Awake()
        {
            if (FindAnyObjectByType<OVRManager>() == null)
            {
                var managerGo = new GameObject("OVRManager");
                managerGo.AddComponent<OVRManager>();
            }

            base.Awake();
        }

        public override bool HasActiveController => OVRInput.GetActiveController() != OVRInput.Controller.None;

        public override bool GetDown(ControllerButton button)
        {
            switch (button)
            {
                case ControllerButton.A:
                    return OVRInput.GetDown(OVRInput.Button.One);
                case ControllerButton.B:
                    return OVRInput.GetDown(OVRInput.Button.Two);
                case ControllerButton.X:
                    return OVRInput.GetDown(OVRInput.Button.Three);
                case ControllerButton.Y:
                    return OVRInput.GetDown(OVRInput.Button.Four);
                case ControllerButton.Menu:
                    return OVRInput.GetDown(OVRInput.Button.Start);
                case ControllerButton.Trigger:
                    return OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) ||
                           OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger);
                case ControllerButton.Grip:
                    return OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger) ||
                           OVRInput.GetDown(OVRInput.Button.SecondaryHandTrigger);
                default:
                    return false;
            }
        }
    }
}
