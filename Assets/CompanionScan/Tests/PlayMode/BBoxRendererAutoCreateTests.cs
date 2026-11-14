using System.Collections;
using CompanionScan.Core;
using CompanionScan.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

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

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rendererGo);
            Object.DestroyImmediate(cameraGo);
        }

        [UnityTest]
        public IEnumerator RenderBoxes_DrawsEachBoxOverItsObject_WithItsLabel()
        {
            // The boxes used to stay inactive, sit off the frame and be all but transparent.
            foreach (var leftover in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (leftover.name == "CameraPreviewCanvas")
                {
                    Object.DestroyImmediate(leftover.gameObject);
                }
            }

            var rendererGo = new GameObject("BBoxRenderer");
            var renderer = rendererGo.AddComponent<BBoxRenderer>();
            var frame = new Texture2D(200, 100, TextureFormat.RGB24, false);

            var detection = new DetectionResult();
            detection.labels.Add("cup");
            detection.bboxes.Add(new[] { 0.1f, 0.2f, 0.4f, 0.6f }); // x1, y1, x2, y2 from the top-left
            detection.scores.Add(0.9f);

            renderer.RenderBoxes(detection, frame);
            yield return null;

            var container = renderer.PreviewCanvas.GetComponentInChildren<RawImage>().transform.GetChild(0) as RectTransform;
            Assert.IsNotNull(container, "no box container");
            RectTransform box = null;
            foreach (RectTransform child in container)
            {
                if (child.gameObject.activeSelf)
                {
                    Assert.IsNull(box, "more boxes than detections are showing");
                    box = child;
                }
            }

            Assert.IsNotNull(box, "the box is not showing");
            var corners = new Vector3[4];
            box.GetWorldCorners(corners);
            var bottomLeft = Normalized(container, corners[0]);
            var topRight = Normalized(container, corners[2]);
            Assert.That(Vector2.Distance(bottomLeft, new Vector2(0.1f, 0.4f)), Is.LessThan(1e-3f), "box bottom-left " + bottomLeft);
            Assert.That(Vector2.Distance(topRight, new Vector2(0.4f, 0.8f)), Is.LessThan(1e-3f), "box top-right " + topRight);

            foreach (var image in box.GetComponentsInChildren<Image>())
            {
                if (image.gameObject != box.gameObject)
                {
                    Assert.Greater(image.color.a, 0.5f, image.name + " edge is see-through");
                }
            }

            Assert.AreEqual("cup", ReadText(box), "the box does not show its label");

            Object.DestroyImmediate(renderer.PreviewCanvas.gameObject);
            Object.DestroyImmediate(rendererGo);
            Object.DestroyImmediate(frame);
        }

        // TextMesh Pro text by reflection: this test assembly does not reference TextMesh Pro.
        private static string ReadText(Component root)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                for (var type = component.GetType(); type != null; type = type.BaseType)
                {
                    if (type.FullName == "TMPro.TMP_Text")
                    {
                        return type.GetProperty("text")?.GetValue(component) as string;
                    }
                }
            }

            return null;
        }

        // A point inside the container, as fractions of its width and height from its bottom-left.
        private static Vector2 Normalized(RectTransform container, Vector3 worldPoint)
        {
            var local = container.InverseTransformPoint(worldPoint);
            var rect = container.rect;
            return new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
        }
    }
}
