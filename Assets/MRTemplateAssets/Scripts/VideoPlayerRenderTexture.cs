using UnityEngine;
using UnityEngine.Video;

namespace Unity.XRTemplate
{
    /// <summary>
    /// Creates a RenderTexture for rendering video to a target renderer.
    /// Handles proper cleanup of created resources.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class VideoPlayerRenderTexture : MonoBehaviour
    {
        const string k_ShaderName = "Unlit/Texture";

        [SerializeField]
        [Tooltip("The target Renderer which will display the video.")]
        Renderer m_Renderer;

        [SerializeField]
        [Tooltip("The width of the RenderTexture which will be created.")]
        int m_RenderTextureWidth = 1920;

        [SerializeField]
        [Tooltip("The height of the RenderTexture which will be created.")]
        int m_RenderTextureHeight = 1080;

        [SerializeField]
        [Tooltip("The bit depth of the depth channel for the RenderTexture which will be created.")]
        int m_RenderTextureDepth;

        RenderTexture m_RenderTexture;
        Material m_Material;

        void Start()
        {
            if (m_Renderer == null)
            {
                Debug.LogWarning($"VideoPlayerRenderTexture: No renderer assigned on {name}", this);
                enabled = false;
                return;
            }

            m_RenderTexture = new RenderTexture(m_RenderTextureWidth, m_RenderTextureHeight, m_RenderTextureDepth);
            m_RenderTexture.Create();

            var shader = Shader.Find(k_ShaderName);
            if (shader == null)
            {
                Debug.LogError($"VideoPlayerRenderTexture: Could not find shader '{k_ShaderName}'", this);
                return;
            }

            m_Material = new Material(shader);
            m_Material.mainTexture = m_RenderTexture;
            GetComponent<VideoPlayer>().targetTexture = m_RenderTexture;
            m_Renderer.material = m_Material;
        }

        void OnDestroy()
        {
            // Clean up created resources to prevent memory leaks
            if (m_RenderTexture != null)
            {
                m_RenderTexture.Release();
                Destroy(m_RenderTexture);
            }

            if (m_Material != null)
            {
                Destroy(m_Material);
            }
        }
    }
}
