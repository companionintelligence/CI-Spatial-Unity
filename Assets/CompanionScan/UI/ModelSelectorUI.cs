using UnityEngine;

namespace CompanionScan.UI
{
    public class ModelSelectorUI : MonoBehaviour
    {
        public enum Mode
        {
            Detect,
            Segment,
            Caption
        }

        [SerializeField] private Mode currentMode = Mode.Detect;

        public Mode CurrentMode => currentMode;

        public void SetMode(int index)
        {
            currentMode = (Mode)Mathf.Clamp(index, 0, 2);
        }
    }
}

