using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Controls visual feedback for pinch gestures by adjusting blend shape weights based on input values.
/// </summary>
public class PinchVizController : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The skinned mesh renderer to control for pinch visualization.")]
    SkinnedMeshRenderer m_Pointer;

    XRRayInteractor m_Interactor;

    void Start()
    {
        m_Interactor = GetComponent<XRRayInteractor>();
        if (m_Interactor == null)
        {
            Debug.LogWarning($"PinchVizController: No XRRayInteractor found on {name}", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (m_Interactor != null && m_Pointer != null)
        {
            var inputValue = Mathf.Max(m_Interactor.selectInput.ReadValue(), m_Interactor.uiPressInput.ReadValue());
            m_Pointer.SetBlendShapeWeight(0, inputValue * 100f);
        }
    }
}
