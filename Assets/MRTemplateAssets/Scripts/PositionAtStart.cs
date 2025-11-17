using System.Collections;
using UnityEngine;

/// <summary>
/// Positions this GameObject relative to a target transform after a startup delay.
/// Useful for initializing UI or objects in XR space relative to the camera.
/// </summary>
public class PositionAtStart : MonoBehaviour
{
    const float k_StartDelay = 2.0f;

    [SerializeField]
    [Tooltip("The target transform to position relative to.")]
    Transform m_Target;

    [SerializeField]
    [Tooltip("Offset from the target position in the target's local space.")]
    Vector3 m_TargetOffset = Vector3.forward;

    [SerializeField]
    [Tooltip("Whether to ignore the X rotation of the target.")]
    bool m_IgnoreX = true;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(k_StartDelay);

        if (m_Target == null)
        {
            Debug.LogWarning($"PositionAtStart: No target assigned on {name}", this);
            yield break;
        }

        var targetRotation = m_Target.rotation;
        var targetEuler = targetRotation.eulerAngles;
        targetRotation = Quaternion.Euler(
            m_IgnoreX ? 0f : targetEuler.x,
            targetEuler.y,
            targetEuler.z
        );

        var targetPosition = m_Target.position + (targetRotation * m_TargetOffset);
        transform.position = targetPosition;
    }
}
