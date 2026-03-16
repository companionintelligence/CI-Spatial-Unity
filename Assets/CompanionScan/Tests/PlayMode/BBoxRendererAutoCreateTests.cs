using CompanionScan.Core;
using CompanionScan.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

namespace CompanionScan.Tests.PlayMode
{
    public class BBoxRendererAutoCreateTests
    {
        [UnityTest]
        public IEnumerator RenderBoxes_CreatesRuntimeUiWhenMissing()
        {
            var cameraGo = new GameObject("PreviewCamera");
            cameraGo.tag = "MainCamera";
            cameraGo.AddComponent<Camera>();

            var rendererGo = new GameObject("BBoxRenderer");
            var renderer = rendererGo.AddComponent<BBoxRenderer>();

            var detection = new DetectionResult();
            detection.labels.Add("box");
            detection.bboxes.Add(new float[] { 0.1f, 0.1f, 0.3f, 0.3f });
            detection.scores.Add(0.9f);

            var tex = new Texture2D(128, 128, TextureFormat.RGB24, false);
            var pixels = new Color32[128 * 128];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            renderer.RenderBoxes(detection, tex);
            yield return null;

            var container = GameObject.Find("BBOX Container") ?? GameObject.Find("BBoxContainer");
            Assert.IsNotNull(container, "Expected runtime BBOX container to be created.");
            Assert.Greater(container.transform.childCount, 0, "Expected at least one bbox instance.");

            var canvas = GameObject.Find("CameraPreviewCanvas")?.GetComponent<Canvas>();
            Assert.IsNotNull(canvas, "Expected preview canvas to be created.");
            Assert.AreEqual(RenderMode.WorldSpace, canvas.renderMode);

            Assert.AreEqual(1, renderer.LastValidation.Count);
            Assert.IsTrue(renderer.LastValidation[0].IsValid);

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rendererGo);
            Object.DestroyImmediate(cameraGo);
        }
    }
}