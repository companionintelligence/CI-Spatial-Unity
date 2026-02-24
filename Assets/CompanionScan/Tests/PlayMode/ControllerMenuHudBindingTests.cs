using CompanionScan.UI;
using NUnit.Framework;
using UnityEngine;

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

            menu.CycleModel();

            var modelLabel = GameObject.Find("ModelLabel");
            Assert.IsNotNull(modelLabel, "Expected HUD model label to be found.");
            var textValue = ReadTmpText(modelLabel);
            Assert.AreEqual("Model: DINO", textValue);

            Object.DestroyImmediate(menuGo);
            Object.DestroyImmediate(hudRoot);
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
