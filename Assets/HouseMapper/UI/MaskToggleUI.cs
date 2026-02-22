using HouseMapper.Rendering;
using UnityEngine;

namespace HouseMapper.UI
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
