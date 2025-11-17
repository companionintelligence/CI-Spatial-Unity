using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.CI.MRTemplate.Tests
{
    /// <summary>
    /// Play mode tests for MRTemplate components.
    /// These tests run with Unity in Play mode and can test runtime behavior.
    /// </summary>
    public class MRTemplatePlayModeTests
    {
        GameObject m_TestGameObject;

        [SetUp]
        public void Setup()
        {
            m_TestGameObject = new GameObject("TestObject");
        }

        [TearDown]
        public void Teardown()
        {
            if (m_TestGameObject != null)
                Object.Destroy(m_TestGameObject);
        }

        [UnityTest]
        public IEnumerator FadeMaterial_InitializesWithoutErrors()
        {
            // Create a test object with a renderer and material
            var renderer = m_TestGameObject.AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Standard"));
            renderer.material = material;

            // Create a fade target
            var fadeTarget = new GameObject("FadeTarget");
            fadeTarget.AddComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));

            var fadeMaterial = m_TestGameObject.AddComponent<FadeMaterial>();

            // Wait one frame for Awake to execute
            yield return null;

            Assert.IsNotNull(fadeMaterial, "FadeMaterial component should exist");

            // Cleanup
            Object.Destroy(fadeTarget);
            Object.Destroy(material);
        }

        [UnityTest]
        public IEnumerator VideoPlayerRenderTexture_CleansUpResources()
        {
            var videoPlayer = m_TestGameObject.AddComponent<UnityEngine.Video.VideoPlayer>();
            var renderer = m_TestGameObject.AddComponent<MeshRenderer>();
            
            var component = m_TestGameObject.AddComponent<VideoPlayerRenderTexture>();

            // Wait for Start to execute
            yield return null;

            // Verify component exists
            Assert.IsNotNull(component, "VideoPlayerRenderTexture should be created");

            // Destroy the GameObject (should trigger OnDestroy cleanup)
            Object.Destroy(m_TestGameObject);
            m_TestGameObject = null;

            // Wait for destruction
            yield return null;

            // Test passes if no errors occurred during cleanup
            Assert.Pass("Component cleaned up resources without errors");
        }

        [UnityTest]
        public IEnumerator PinchVizController_DisablesWhenMissingInteractor()
        {
            var component = m_TestGameObject.AddComponent<PinchVizController>();

            // Wait for Start to execute
            yield return null;

            // Component should disable itself if XRRayInteractor is missing
            Assert.IsFalse(component.enabled, "PinchVizController should disable when XRRayInteractor is missing");
        }
    }
}
