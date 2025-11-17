using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Controls the fade animation of plane materials including dot radius and alpha values.
/// </summary>
public class FadePlaneMaterial : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The plane GameObject whose material will be faded.")]
    GameObject m_Plane;

    [SerializeField]
    [Tooltip("Duration of the dot fade effect in seconds.")]
    float m_FadeSpeed = 1f;

    [SerializeField]
    [Tooltip("Duration of the alpha fade effect in seconds.")]
    float m_FadeAlphaSpeed = 0.5f;

    [SerializeField]
    [Tooltip("Maximum view radius for the dot effect.")]
    float m_ViewRadius = 2.5f;

    [SerializeField]
    [Tooltip("Target alpha value when visible.")]
    float m_Alpha = 0.5f;

    Coroutine m_FadeDotsCoroutine;
    Coroutine m_FadeAlphaCoroutine;
    Renderer m_CachedRenderer;
    static readonly int k_DotViewRadius = Shader.PropertyToID("_DotViewRadius");
    static readonly int k_Alpha = Shader.PropertyToID("_Alpha");

    void Awake()
    {
        if (m_Plane != null)
        {
            m_CachedRenderer = m_Plane.GetComponent<Renderer>();
            if (m_CachedRenderer != null)
            {
                m_CachedRenderer.material.SetFloat(k_DotViewRadius, 0f);
                m_CachedRenderer.material.SetFloat(k_Alpha, 0f);
            }
        }
        FadePlane(true);
    }

    /// <summary>
    /// Initiates a fade effect on the plane material.
    /// </summary>
    /// <param name="visible">True to fade in, false to fade out.</param>
    public void FadePlane(bool visible)
    {
        if (m_FadeDotsCoroutine != null)
            StopCoroutine(m_FadeDotsCoroutine);

        if (m_FadeAlphaCoroutine != null)
            StopCoroutine(m_FadeAlphaCoroutine);

        m_FadeAlphaCoroutine = StartCoroutine(FadeAlpha(visible));
        m_FadeDotsCoroutine = StartCoroutine(FadeDots(visible));
    }

    /// <summary>
    /// Coroutine that fades the dot view radius.
    /// </summary>
    IEnumerator FadeDots(bool visible)
    {
        yield return new WaitForSeconds(m_FadeAlphaSpeed);

        if (m_CachedRenderer == null)
        {
            Debug.LogWarning($"FadePlaneMaterial: No renderer found on {name}", this);
            yield break;
        }

        float viewRadiusValue = m_CachedRenderer.material.GetFloat(k_DotViewRadius);

        if (!visible)
        {
            // Fade out dots
            while (m_CachedRenderer.material.GetFloat(k_DotViewRadius) > 0f)
            {
                viewRadiusValue -= Time.deltaTime / m_FadeSpeed;
                m_CachedRenderer.material.SetFloat(k_DotViewRadius, viewRadiusValue);
                yield return null;
            }
            m_CachedRenderer.material.SetFloat(k_DotViewRadius, 0f);
        }
        else
        {
            // Fade in dots
            while (m_CachedRenderer.material.GetFloat(k_DotViewRadius) < m_ViewRadius)
            {
                viewRadiusValue += Time.deltaTime / m_FadeSpeed;
                m_CachedRenderer.material.SetFloat(k_DotViewRadius, viewRadiusValue);
                yield return null;
            }
            m_CachedRenderer.material.SetFloat(k_DotViewRadius, m_ViewRadius);
        }
    }

    /// <summary>
    /// Coroutine that fades the alpha value.
    /// </summary>
    IEnumerator FadeAlpha(bool visible)
    {
        if (m_CachedRenderer == null)
        {
            Debug.LogWarning($"FadePlaneMaterial: No renderer found on {name}", this);
            yield break;
        }

        float alphaValue = m_CachedRenderer.material.GetFloat(k_Alpha);

        if (!visible)
        {
            // Fade out alpha
            while (m_CachedRenderer.material.GetFloat(k_Alpha) > 0f)
            {
                alphaValue -= Time.deltaTime / m_FadeAlphaSpeed;
                m_CachedRenderer.material.SetFloat(k_Alpha, alphaValue);
                yield return null;
            }
            m_CachedRenderer.material.SetFloat(k_Alpha, 0f);
        }
        else
        {
            // Fade in alpha
            while (m_CachedRenderer.material.GetFloat(k_Alpha) < m_Alpha)
            {
                alphaValue += Time.deltaTime / m_FadeAlphaSpeed;
                m_CachedRenderer.material.SetFloat(k_Alpha, alphaValue);
                yield return null;
            }
            m_CachedRenderer.material.SetFloat(k_Alpha, m_Alpha);
        }
    }
}
