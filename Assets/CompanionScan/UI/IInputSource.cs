namespace CompanionScan.UI
{
    /// <summary>
    /// The buttons the app uses, named for a Touch controller. Another backend maps its own inputs
    /// (a screen button, a hand pinch, a puck button) onto them.
    /// </summary>
    public enum ControllerButton
    {
        /// <summary>Right controller, lower face button (Touch A, OVR Button.One).</summary>
        A,

        /// <summary>Right controller, upper face button (Touch B, Button.Two).</summary>
        B,

        /// <summary>Left controller, lower face button (Touch X, Button.Three).</summary>
        X,

        /// <summary>Left controller, upper face button (Touch Y, Button.Four).</summary>
        Y,

        /// <summary>The menu button (left controller, Button.Start).</summary>
        Menu,

        /// <summary>The index trigger on either hand.</summary>
        Trigger,

        /// <summary>The grip (hand) trigger on either hand.</summary>
        Grip
    }

    /// <summary>
    /// An input backend: OVRInput on Quest, the XR Interaction Toolkit, touch on a phone, a scripted
    /// source in tests. <see cref="ControllerInput"/> routes what it reports to the menu and the panels.
    /// </summary>
    public interface IInputSource
    {
        /// <summary>True while an input device the user can press is active (a controller is on).</summary>
        bool HasActiveController { get; }

        /// <summary>True during the frame <paramref name="button"/> went down.</summary>
        bool GetDown(ControllerButton button);
    }
}
