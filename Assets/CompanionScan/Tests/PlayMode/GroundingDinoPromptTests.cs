using System.Collections.Generic;
using System.Reflection;
using CompanionScan.Core;
using CompanionScan.Mapping;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Tests.PlayMode
{
    /// <summary>
    /// What GroundingDINO is asked to find when Florence has not listed the snapshot's objects, which
    /// with the GDino preset the app starts on is every capture. The prompt was "furniture . object .",
    /// so every vision server pin was labelled furniture or object.
    /// </summary>
    public class GroundingDinoPromptTests
    {
        private GameObject _go;
        private SpatialCapturePipeline _pipeline;

        [SetUp]
        public void SetUp()
        {
            // Inactive, so it sets up no camera: only its prompts are read.
            _go = new GameObject("Pipeline");
            _go.SetActive(false);
            _pipeline = _go.AddComponent<SpatialCapturePipeline>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void WithoutFlorenceLabels_GroundingDinoAndSam2_AskForCommonRoomObjects()
        {
            Assert.AreEqual(SpatialCapturePipeline.DefaultGroundingDinoPrompt, DinoPrompt(null));
            Assert.AreEqual(SpatialCapturePipeline.DefaultGroundingDinoPrompt, DinoPrompt(new List<string>()));

            // SAM2's last fallback: GroundingDINO found nothing that scored high enough (or did not
            // run) and Florence listed nothing.
            var weak = new DetectionResult();
            weak.labels.Add("cup");
            weak.scores.Add(0.05f);
            weak.bboxes.Add(new[] { 0.4f, 0.4f, 0.6f, 0.6f });
            Assert.AreEqual(SpatialCapturePipeline.DefaultGroundingDinoPrompt, SamPrompt(null, null));
            Assert.AreEqual(SpatialCapturePipeline.DefaultGroundingDinoPrompt, SamPrompt(weak, null));

            // Florence's labels, when it lists them, still make the prompt.
            Assert.AreEqual("couch . lamp .", DinoPrompt(new List<string> { "couch", "lamp" }));
        }

        [Test]
        public void ABlankPromptField_AsksForCommonRoomObjects([Values("", "   ", null)] string blank)
        {
            SetPrivateField(_pipeline, "groundingDinoFallbackPrompt", blank);

            Assert.AreEqual(SpatialCapturePipeline.DefaultGroundingDinoPrompt, _pipeline.GroundingDinoFallbackPrompt);
            Assert.AreEqual(SpatialCapturePipeline.DefaultGroundingDinoPrompt, DinoPrompt(null));
            Assert.AreEqual(SpatialCapturePipeline.DefaultGroundingDinoPrompt, SamPrompt(null, null));
        }

        [Test]
        public void AnotherPrompt_IsSentWithoutItsOuterSpaces()
        {
            SetPrivateField(_pipeline, "groundingDinoFallbackPrompt", "  lamp . cup .  ");

            Assert.AreEqual("lamp . cup .", DinoPrompt(null));
            Assert.AreEqual("lamp . cup .", SamPrompt(null, null));
        }

        private string DinoPrompt(List<string> florenceLabels)
        {
            return (string)Method("BuildDinoPrompt").Invoke(_pipeline, new object[] { florenceLabels });
        }

        private string SamPrompt(DetectionResult detection, List<string> florenceLabels)
        {
            return (string)Method("BuildSamPrompt").Invoke(_pipeline, new object[] { detection, florenceLabels });
        }

        private static MethodInfo Method(string name)
        {
            var method = typeof(SpatialCapturePipeline).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"SpatialCapturePipeline.{name} not found");
            return method;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} not found");
            field.SetValue(target, value);
        }
    }
}
