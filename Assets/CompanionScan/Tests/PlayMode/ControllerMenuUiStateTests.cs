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
        public void CycleDisplayMode_TogglesRenderersAndPins()
        {
            var pipelineGo = new GameObject("Pipeline");
            var pipeline = pipelineGo.AddComponent<SpatialCapturePipeline>();

            var bboxGo = new GameObject("BBoxRenderer");
            var bbox = bboxGo.AddComponent<BBoxRenderer>();

            var labelGo = new GameObject("LabelRenderer");
            var label = labelGo.AddComponent<LabelRenderer>();

            var pinGo = new GameObject("PinManager");
            var pinManager = pinGo.AddComponent<SpatialPinManager>();

            var menuGo = new GameObject("MenuUI");
            var menu = menuGo.AddComponent<ControllerMenuUI>();
            SetPrivateField(menu, "pipeline", pipeline);
            SetPrivateField(menu, "bboxRenderer", bbox);
            SetPrivateField(menu, "labelRenderer", label);
            SetPrivateField(menu, "pinManager", pinManager);

            menu.SetDisplayMode(AnchorDisplayMode.BoundingBox2D);
            Assert.IsTrue(bbox.gameObject.activeSelf);
            Assert.IsFalse(label.gameObject.activeSelf);
            Assert.IsFalse(pinManager.gameObject.activeSelf);
            Assert.IsTrue(pipeline.RenderBoxes);
            Assert.IsTrue(pipeline.RenderMasks);

            menu.SetDisplayMode(AnchorDisplayMode.SpatialLabel3D);
            Assert.IsFalse(bbox.gameObject.activeSelf);
            Assert.IsTrue(label.gameObject.activeSelf);
            Assert.IsTrue(pinManager.gameObject.activeSelf);
            Assert.IsFalse(pipeline.RenderBoxes);
            Assert.IsTrue(pipeline.RenderMasks);

            menu.SetDisplayMode(AnchorDisplayMode.Both);
            Assert.IsTrue(bbox.gameObject.activeSelf);
            Assert.IsTrue(label.gameObject.activeSelf);
            Assert.IsTrue(pinManager.gameObject.activeSelf);
            Assert.IsTrue(pipeline.RenderBoxes);
            Assert.IsTrue(pipeline.RenderMasks);

            Object.DestroyImmediate(menuGo);
            Object.DestroyImmediate(pinGo);
            Object.DestroyImmediate(labelGo);
            Object.DestroyImmediate(bboxGo);
            Object.DestroyImmediate(pipelineGo);
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
