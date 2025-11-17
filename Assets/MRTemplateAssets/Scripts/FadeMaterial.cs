using System.Collections;
using UnityEngine;

/// <summary>
/// Fades a material's alpha value over time for smooth visibility transitions.
/// </summary>
public class FadeMaterial : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The GameObject whose material will be faded.")]
    GameObject m_Environment;

    [SerializeField]
    [Tooltip("Duration of the fade effect in seconds.")]
    float m_FadeSpeed = 1f;

    Coroutine m_FadeCoroutine;
    Renderer m_CachedRenderer;

    void Awake()
    {
        if (m_Environment != null)
        {
            m_CachedRenderer = m_Environment.GetComponent<Renderer>();
        }
    }

    /// <summary>
    /// Fades the skybox material to visible or invisible.
    /// </summary>
    /// <param name="visible">True to fade to invisible, false to fade to visible.</param>
    public void FadeSkybox(bool visible)
    {
        if (m_FadeCoroutine != null)
            StopCoroutine(m_FadeCoroutine);

        m_FadeCoroutine = StartCoroutine(Fade(visible));
    }

    /// <summary>
    /// Coroutine that performs the fade animation.
    /// </summary>
    IEnumerator Fade(bool visible)
    {
        if (m_CachedRenderer == null)
        {
            Debug.LogWarning($"FadeMaterial: No renderer found on {name}", this);
            yield break;
        }

        float alphaValue = m_CachedRenderer.material.GetFloat("_Alpha");

        if (visible)
        {
            // Fade to invisible
            while (m_CachedRenderer.material.GetFloat("_Alpha") > 0f)
            {
                alphaValue -= Time.deltaTime / m_FadeSpeed;
                m_CachedRenderer.material.SetFloat("_Alpha", alphaValue);
                yield return null;
            }
            m_CachedRenderer.material.SetFloat("_Alpha", 0f);
        }
        else
        {
            // Fade to visible
            while (m_CachedRenderer.material.GetFloat("_Alpha") < 1f)
            {
                alphaValue += Time.deltaTime / m_FadeSpeed;
                m_CachedRenderer.material.SetFloat("_Alpha", alphaValue);
                yield return null;
            }
            m_CachedRenderer.material.SetFloat("_Alpha", 1f);
        }
    }
}
