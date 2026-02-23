using TMPro;
using UnityEngine;

namespace CompanionScan.Rendering
{
    public class CaptionPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text captionText;

        public void SetCaption(string text)
        {
            if (captionText != null)
            {
                captionText.text = text;
            }
        }
    }
}

