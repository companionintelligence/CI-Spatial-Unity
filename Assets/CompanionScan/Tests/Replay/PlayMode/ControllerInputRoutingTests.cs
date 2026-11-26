using System.Collections.Generic;
using System.Reflection;
using CompanionScan.Mapping;
using CompanionScan.Replay;
using CompanionScan.UI;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Replay.Tests
{
    /// <summary>
    /// What the buttons do, with a scripted input backend in place of OVRInput. The routing used to live
    /// in the Quest-only input class, where nothing could drive it.
    /// </summary>
    public class ControllerInputRoutingTests
    {
        private readonly List<Object> _created = new List<Object>();
        private ControllerMenuUI _menu;
        private SpatialCapturePipeline _pipeline;
        private ScriptedControllerInput _input;

        [SetUp]
        public void SetUp()
        {
            var pipelineGo = new GameObject("Pipeline");
            pipelineGo.SetActive(false);
            _created.Add(pipelineGo);
            _pipeline = pipelineGo.AddComponent<SpatialCapturePipeline>();

            var menuGo = new GameObject("MenuUI");
            _created.Add(menuGo);
            _menu = menuGo.AddComponent<ControllerMenuUI>();
            SetPrivateField(_menu, "pipeline", _pipeline);

            var inputGo = new GameObject("Input");
            _created.Add(inputGo);
            _input = inputGo.AddComponent<ScriptedControllerInput>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void B_CyclesTheModel()
        {
            Assert.AreEqual(ControllerMenuUI.ModelPreset.GDino, _menu.ActiveModel);

            _input.PressAndTick(ControllerButton.B);

            Assert.AreEqual(ControllerMenuUI.ModelPreset.SAM2, _menu.ActiveModel);
        }

        [Test]
        public void X_CyclesThePreviewDistance()
        {
            var before = _menu.CurrentPreviewDistance;

            _input.PressAndTick(ControllerButton.X);

            Assert.AreNotEqual(before, _menu.CurrentPreviewDistance);
        }

        [Test]
        public void EitherTrigger_CyclesTheDisplayMode()
        {
            var before = _menu.DisplayMode;

            _input.PressAndTick(ControllerButton.Trigger);

            Assert.AreNotEqual(before, _menu.DisplayMode);
        }

        [Test]
        public void NoButton_ChangesNothing()
        {
            var model = _menu.ActiveModel;
            var distance = _menu.CurrentPreviewDistance;
            var display = _menu.DisplayMode;

            _input.PressAndTick();

            Assert.AreEqual(model, _menu.ActiveModel);
            Assert.AreEqual(distance, _menu.CurrentPreviewDistance);
            Assert.AreEqual(display, _menu.DisplayMode);
        }

        [Test]
        public void AButtonIsReportedForOneFrameOnly()
        {
            _input.PressAndTick(ControllerButton.B);
            var afterOnePress = _menu.ActiveModel;

            _input.PressAndTick();

            Assert.AreEqual(afterOnePress, _menu.ActiveModel, "the press carried over to the next frame");
            Assert.IsFalse(_input.GetDown(ControllerButton.B));
        }

        [Test]
        public void SeveralButtonsInOneFrame_AreAllRouted()
        {
            var distance = _menu.CurrentPreviewDistance;

            _input.PressAndTick(ControllerButton.B, ControllerButton.X);

            Assert.AreEqual(ControllerMenuUI.ModelPreset.SAM2, _menu.ActiveModel);
            Assert.AreNotEqual(distance, _menu.CurrentPreviewDistance);
        }

        [Test]
        public void TheInputFindsTheMenuInTheSceneWhenItWasNotGivenOne()
        {
            // The menu is created in SetUp before the input, so Awake found it. A menu that appears later is found on the next tick.
            Object.DestroyImmediate(_input.gameObject);
            var late = new GameObject("LateInput");
            _created.Add(late);
            var lateInput = late.AddComponent<ScriptedControllerInput>();
            Assert.AreSame(_menu, GetMenu(lateInput));

            lateInput.PressAndTick(ControllerButton.B);

            Assert.AreEqual(ControllerMenuUI.ModelPreset.SAM2, _menu.ActiveModel);
        }

        [Test]
        public void ANoControllerWarning_IsLoggedOnce()
        {
            int warnings = 0;
            void Count(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Warning && message.Contains("No active controller"))
                {
                    warnings++;
                }
            }

            _input.ControllerActive = false;
            Application.logMessageReceived += Count;
            try
            {
                _input.PressAndTick();
                _input.PressAndTick();
                _input.PressAndTick();
            }
            finally
            {
                Application.logMessageReceived -= Count;
            }

            Assert.AreEqual(1, warnings);
        }

        // The menu field is protected in the base class, and the builder sets it by name on the subclass.
        [Test]
        public void TheMenuFieldCanBeSetByNameOnTheSubclass_AsControllerMenuBuilderDoesIt()
        {
            var field = typeof(ScriptedControllerInput).GetField("menuUI", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(field, "reflection on the subclass does not find the menu field");
        }

        private static ControllerMenuUI GetMenu(ControllerInput input)
        {
            var field = typeof(ControllerInput).GetField("menuUI", BindingFlags.NonPublic | BindingFlags.Instance);
            return (ControllerMenuUI)field.GetValue(input);
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} not found");
            field.SetValue(target, value);
        }
    }
}
