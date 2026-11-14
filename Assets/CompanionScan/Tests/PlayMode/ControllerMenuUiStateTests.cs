using System.Reflection;
using CompanionScan.Mapping;
using CompanionScan.Rendering;
using CompanionScan.UI;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Tests.PlayMode
{
    public class ControllerMenuUiStateTests
    {
        [Test]
        public void CycleModel_UpdatesPipelineFlags()
        {
            var pipelineGo = new GameObject("Pipeline");
            var pipeline = pipelineGo.AddComponent<SpatialCapturePipeline>();

            var menuGo = new GameObject("MenuUI");
            var menu = menuGo.AddComponent<ControllerMenuUI>();
            SetPrivateField(menu, "pipeline", pipeline);

            menu.CycleModel();
            Assert.IsFalse(pipeline.RunFlorence);
            Assert.IsTrue(pipeline.RunGroundingDino);
            Assert.IsFalse(pipeline.RunSam2);

            menu.CycleModel();
            Assert.IsFalse(pipeline.RunFlorence);
            Assert.IsFalse(pipeline.RunGroundingDino);
            Assert.IsTrue(pipeline.RunSam2);

            menu.CycleModel();
            Assert.IsTrue(pipeline.RunFlorence);
            Assert.IsFalse(pipeline.RunGroundingDino);
            Assert.IsFalse(pipeline.RunSam2);

            Object.DestroyImmediate(menuGo);
            Object.DestroyImmediate(pipelineGo);
        }

        [Test]
        public void TheTrigger_CyclesBoxesLabelsBoth()
        {
            var menuGo = new GameObject("MenuUI");
            var menu = menuGo.AddComponent<ControllerMenuUI>();

            Assert.AreEqual(AnchorDisplayMode.Both, menu.DisplayMode);
            menu.CycleDisplayMode();
            Assert.AreEqual(AnchorDisplayMode.BoundingBox2D, menu.DisplayMode);
            menu.CycleDisplayMode();
            Assert.AreEqual(AnchorDisplayMode.SpatialLabel3D, menu.DisplayMode);
            menu.CycleDisplayMode();
            Assert.AreEqual(AnchorDisplayMode.Both, menu.DisplayMode);

            Object.DestroyImmediate(menuGo);
        }

        [Test]
        public void DisplayModes_ShowBoxesLabelsOrBoth_WithoutSwitchingOffCapture()
        {
            // Laid out like the shipped scene: one object runs capture and every renderer, and the
            // controller input sits on a child of it. The boxes mode used to deactivate that object,
            // which stopped capture and every button, the trigger that would switch back included.
            var captureGo = new GameObject("CompanionScan");
            captureGo.SetActive(false);
            var pipeline = captureGo.AddComponent<SpatialCapturePipeline>();
            SetPrivateField(pipeline, "preferArCameraOnQuest", false);
            var bbox = captureGo.AddComponent<BBoxRenderer>();
            var labels = captureGo.AddComponent<LabelRenderer>();
            var pins = captureGo.AddComponent<SpatialPinManager>();
            SetPrivateField(pipeline, "pinManager", pins);
            SetPrivateField(pipeline, "labelRenderer", labels);
            SetPrivateField(pipeline, "bboxRenderer", bbox);

            var pinPrefab = new GameObject("TestPin");
            var labelPrefab = new GameObject("TestLabel");
            labelPrefab.AddComponent<SpatialLabelView>();
            SetPrivateField(pins, "pinPrefab", pinPrefab);
            SetPrivateField(labels, "labelPrefab", labelPrefab);

            // The camera preview panel the boxes are drawn on.
            var previewGo = new GameObject("PreviewCanvas", typeof(RectTransform));
            var previewCanvas = previewGo.AddComponent<Canvas>();
            previewCanvas.renderMode = RenderMode.WorldSpace;
            var container = new GameObject("Container", typeof(RectTransform));
            container.transform.SetParent(previewGo.transform, false);
            SetPrivateField(bbox, "container", container.GetComponent<RectTransform>());

            var menuGo = new GameObject("ControllerUI");
            menuGo.transform.SetParent(captureGo.transform, false);
            var menu = menuGo.AddComponent<ControllerMenuUI>();
            SetPrivateField(menu, "pipeline", pipeline);
            SetPrivateField(menu, "bboxRenderer", bbox);
            SetPrivateField(menu, "labelRenderer", labels);
            SetPrivateField(menu, "pinManager", pins);

            captureGo.SetActive(true);
            pins.AddPin(new SpatialPin
            {
                label = "chair",
                semanticLabel = "CHAIR",
                source = PinSource.CvServer,
                position = new Vector3(0f, 0.45f, 2f),
                trackId = 1
            });
            var pinObject = pins.PinObjects[0];
            Assert.IsTrue(labels.TryGetLabel(pins.Pins[0], out var labelObject));

            menu.SetDisplayMode(AnchorDisplayMode.BoundingBox2D);
            AssertCaptureKeepsRunning(captureGo, pipeline, menu);
            Assert.IsTrue(previewCanvas.enabled, "boxes mode hid the boxes");
            Assert.IsFalse(pinObject.activeSelf, "boxes mode shows the pins");
            Assert.IsFalse(labelObject.activeSelf, "boxes mode shows the labels");

            menu.SetDisplayMode(AnchorDisplayMode.SpatialLabel3D);
            AssertCaptureKeepsRunning(captureGo, pipeline, menu);
            Assert.IsFalse(previewCanvas.enabled, "labels mode shows the boxes");
            Assert.IsTrue(pinObject.activeSelf, "labels mode hid the pins");
            Assert.IsTrue(labelObject.activeSelf, "labels mode hid the labels");

            menu.SetDisplayMode(AnchorDisplayMode.Both);
            AssertCaptureKeepsRunning(captureGo, pipeline, menu);
            Assert.IsTrue(previewCanvas.enabled);
            Assert.IsTrue(pinObject.activeSelf);
            Assert.IsTrue(labelObject.activeSelf);

            // Boxes keep being drawn, so the preview's boxes always belong to its frame.
            Assert.IsTrue(pipeline.RenderBoxes);

            Object.DestroyImmediate(captureGo);
            Object.DestroyImmediate(previewGo);
            Object.DestroyImmediate(pinPrefab);
            Object.DestroyImmediate(labelPrefab);
        }

        [Test]
        public void APreviewPanelMadeWhileTheBoxesAreHidden_StartsHidden()
        {
            foreach (var leftover in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (leftover.name == "CameraPreviewCanvas")
                {
                    Object.DestroyImmediate(leftover.gameObject);
                }
            }

            var bboxGo = new GameObject("BBoxRenderer");
            var bbox = bboxGo.AddComponent<BBoxRenderer>();
            bbox.Visible = false;

            var frame = new Texture2D(64, 48, TextureFormat.RGB24, false);
            bbox.SetPreviewTexture(frame);

            Assert.IsNotNull(bbox.PreviewCanvas, "no preview panel was made");
            Assert.IsFalse(bbox.PreviewCanvas.enabled);

            bbox.Visible = true;
            Assert.IsTrue(bbox.PreviewCanvas.enabled);

            Object.DestroyImmediate(bbox.PreviewCanvas.gameObject);
            Object.DestroyImmediate(bboxGo);
            Object.DestroyImmediate(frame);
        }

        private static void AssertCaptureKeepsRunning(GameObject captureGo, SpatialCapturePipeline pipeline, ControllerMenuUI menu)
        {
            Assert.IsTrue(captureGo.activeInHierarchy, "the display mode switched off the capture object");
            Assert.IsTrue(pipeline.isActiveAndEnabled, "the display mode stopped capture");
            Assert.IsTrue(menu.isActiveAndEnabled, "the display mode switched off the controls");
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                Assert.Fail("Missing field: " + fieldName);
            }
            field.SetValue(target, value);
        }
    }
}
