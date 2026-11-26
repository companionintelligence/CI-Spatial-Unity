using System.Collections.Generic;
using CompanionScan.UI;

namespace CompanionScan.Replay
{
    /// <summary>
    /// The input backend of a recorded session and of tests: buttons go down when a script says so. It is
    /// driven by hand: <see cref="PressAndTick"/> reports the buttons for one frame of routing, then
    /// releases them.
    /// </summary>
    public class ScriptedControllerInput : ControllerInput
    {
        private readonly HashSet<ControllerButton> _down = new HashSet<ControllerButton>();

        /// <summary>What <see cref="HasActiveController"/> reports.</summary>
        public bool ControllerActive { get; set; } = true;

        protected override bool TicksEveryFrame => false;

        public override bool HasActiveController => ControllerActive;

        public override bool GetDown(ControllerButton button) => _down.Contains(button);

        /// <summary>Runs one frame of routing with these buttons down, then lets go of them.</summary>
        public void PressAndTick(params ControllerButton[] buttons)
        {
            _down.Clear();
            foreach (var button in buttons)
            {
                _down.Add(button);
            }

            try
            {
                Tick();
            }
            finally
            {
                _down.Clear();
            }
        }
    }
}
