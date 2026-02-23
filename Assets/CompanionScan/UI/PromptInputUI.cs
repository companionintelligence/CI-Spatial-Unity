using TMPro;
using UnityEngine;

namespace CompanionScan.UI
{
    public class PromptInputUI : MonoBehaviour
    {
        [SerializeField] private TMP_InputField inputField;

        public string CurrentPrompt => inputField != null ? inputField.text : string.Empty;
    }
}

