using System.Collections;
using System.Reflection;
using CompanionScan.Core;
using CompanionScan.Mapping;
using CompanionScan.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CompanionScan.Tests.PlayMode
{
    public class ControllerMenuHudBindingTests
    {
        [Test]
        public void CycleModel_UpdatesHudLabel_WhenLabelsAutoFound()
        {
            var hudRoot = new GameObject("ControllerHUD");
            var canvas = hudRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            CreateLabel(hudRoot.transform, "ModelLabel");
            CreateLabel(hudRoot.transform, "DisplayLabel");
            CreateLabel(hudRoot.transform, "PreviewLabel");
            CreateLabel(hudRoot.transform, "BusyLabel");

            var menuGo = new GameObject("MenuUI");
            var menu = menuGo.AddComponent<ControllerMenuUI>();

            var modelLabel = GameObject.Find("ModelLabel");
            Assert.IsNotNull(modelLabel, "Expected HUD model label to be found.");
            Assert.AreEqual("Model: DINO", ReadTmpText(modelLabel), "the menu starts on GDino");

            menu.CycleModel();
            Assert.AreEqual("Model: SAM2", ReadTmpText(modelLabel));

            Object.DestroyImmediate(menuGo);
            Object.DestroyImmediate(hudRoot);
        }

        [Test]
        public void TheWristHud_ShowsDino_FromTheStart()
        {
            // As in the shipped scene: the menu is enabled before ControllerMenuBuilder makes the wrist
            // HUD in its Start. The HUD showed the builder's "Model: All" until B, X or a trigger was
            // pressed.
            // Inactive, so the pipeline sets up no camera. Its server config has no URL, as on a first run.
            var config = ScriptableObject.CreateInstance<ServerConfig>();
            var pipelineGo = new GameObject("Pipeline");
            pipelineGo.SetActive(false);
            var pipeline = pipelineGo.AddComponent<SpatialCapturePipeline>();

            var go = new GameObject("ControllerUI");
            go.SetActive(false);
            var menu = go.AddComponent<ControllerMenuUI>();
            var builder = go.AddComponent<ControllerMenuBuilder>();

            try
            {
                SetPrivateField(pipeline, "serverConfig", config);
                SetPrivateField(menu, "pipeline", pipeline);
                go.SetActive(true);

                SetPrivateField(builder, "_menuUI", menu);
                var buildHud = typeof(ControllerMenuBuilder).GetMethod("BuildHUD",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(buildHud, "ControllerMenuBuilder.BuildHUD not found");
                buildHud.Invoke(builder, null);

                var modelLabel = FindChild(go, "ModelLabel");
                Assert.IsNotNull(modelLabel, "the builder made no model label");
                Assert.AreEqual("Model: DINO", ReadTmpText(modelLabel));

                // The status line keeps the builder's placeholder. Nothing updates it when Settings
                // saves the server URL or a capture ends, so filled in here it read "Server: not set
                // (Menu)" on a first run and kept saying so after the URL was saved.
                var statusLabel = FindChild(go, "StatusLabel");
                Assert.IsNotNull(statusLabel, "the builder made no status label");
                Assert.AreEqual("Raycast: --", ReadTmpText(statusLabel));

                // A button still fills it in, as before.
                menu.CyclePreviewDistance();
                StringAssert.StartsWith("Raycast: PoseOnly | Fallback: 0 | ", ReadTmpText(statusLabel));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(pipelineGo);
                Object.DestroyImmediate(config);
            }
        }

        [UnityTest]
        public IEnumerator BusyLabel_ReadsBusyWithThreeDots_WhileACaptureRuns()
        {
            // The commit "fix(hud): show BUSY... instead of mis-encoded text" fixed this label: it held
            // an ellipsis encoded twice (UTF-8 read as Windows-1252), so the wrist showed BUSY and three
            // stray characters.
            var hudRoot = new GameObject("ControllerHUD");
            hudRoot.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var busyLabel = CreateLabel(hudRoot.transform, "BusyLabel");

            // Inactive, so it sets up no camera: the wrist display reads only whether it is busy.
            var pipelineGo = new GameObject("Pipeline");
            pipelineGo.SetActive(false);
            var pipeline = pipelineGo.AddComponent<SpatialCapturePipeline>();

            var menuGo = new GameObject("MenuUI");
            menuGo.SetActive(false);
            var menu = menuGo.AddComponent<ControllerMenuUI>();
            SetPrivateField(menu, "pipeline", pipeline);
            SetPrivateField(menu, "busyLabel", busyLabel);
            menuGo.SetActive(true);

            try
            {
                SetPrivateField(pipeline, "_isBusy", true);
                yield return null;
                Assert.AreEqual("BUSY...", ReadTmpText(busyLabel.gameObject));

                SetPrivateField(pipeline, "_isBusy", false);
                yield return null;
                Assert.AreEqual(string.Empty, ReadTmpText(busyLabel.gameObject));
            }
            finally
            {
                Object.DestroyImmediate(menuGo);
                Object.DestroyImmediate(pipelineGo);
                Object.DestroyImmediate(hudRoot);
            }
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} not found");
            field.SetValue(target, value);
        }

        private static GameObject FindChild(GameObject root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static Component CreateLabel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmpType = ResolveTmpType();
            Assert.IsNotNull(tmpType, "TextMeshProUGUI type not found.");
            return go.AddComponent(tmpType);
        }

        private static string ReadTmpText(GameObject go)
        {
            var tmpType = ResolveTmpType();
            if (tmpType == null)
            {
                return null;
            }

            var component = go.GetComponent(tmpType);
            if (component == null)
            {
                return null;
            }

            var textProp = tmpType.GetProperty("text");
            return textProp?.GetValue(component) as string;
        }

        private static System.Type ResolveTmpType()
        {
            var type = System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            if (type != null)
            {
                return type;
            }

            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType("TMPro.TextMeshProUGUI");
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
