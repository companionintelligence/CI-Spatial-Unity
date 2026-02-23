using CompanionScan.Rendering;
using UnityEngine;

namespace CompanionScan.UI
{
    public class MaskToggleUI : MonoBehaviour
    {
        [SerializeField] private MaskRenderer maskRenderer;

        public void SetEnabled(bool isOn)
        {
            if (maskRenderer != null)
            {
                maskRenderer.gameObject.SetActive(isOn);
            }
        }
    }
}

