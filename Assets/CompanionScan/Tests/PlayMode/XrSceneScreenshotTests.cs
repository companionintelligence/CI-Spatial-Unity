using System.Collections;
using System.IO;
using System.Reflection;
using CompanionScan.Mapping;
using CompanionScan.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CompanionScan.Tests.PlayMode
{
    public class XrSceneScreenshotTests
    {
        [UnityTest]
        public IEnumerator QaXrScene_CapturesControllersAndAnchors()
        {
            var root = new GameObject("QaXrSceneRoot");

            var cameraGo = new GameObject("QaCamera");
            var camera = cameraGo.AddComponent<Camera>();
            cameraGo.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 1.4f, -2.2f);
            camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 1f);

            var lightGo = new GameObject("QaLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "QaFloor";
            floor.transform.position = new Vector3(0f, 0f, 0f);
            floor.transform.localScale = new Vector3(0.2f, 1f, 0.2f);

            var leftController = CreateController("LeftController", new Vector3(-0.25f, 1.2f, -0.4f));
            var rightController = CreateController("RightController", new Vector3(0.25f, 1.2f, -0.4f));

            CreateAnchor("AnchorA", new Vector3(-0.4f, 1.0f, 0.7f));
            CreateAnchor("AnchorB", new Vector3(0.0f, 1.05f, 0.8f));
            CreateAnchor("AnchorC", new Vector3(0.4f, 0.95f, 0.7f));

            var frustum = root.AddComponent<SnapshotFrustumVisualizer>();
            SetPrivateField(frustum, "targetCamera", camera);
            frustum.FreezeFrustum(2.0f);

            var builder = root.AddComponent<ControllerMenuBuilder>();
            SetPrivateField(builder, "leftAnchor", leftController.transform);
            SetPrivateField(builder, "rightAnchor", rightController.transform);

            yield return null;
            yield return new WaitForEndOfFrame();

            var screenshotPath = QaTestUtils.CaptureScreenshotPng("qa_xr_scene");
            Assert.IsTrue(File.Exists(screenshotPath), "Screenshot was not written.");
            Assert.Greater(new FileInfo(screenshotPath).Length, 0, "Screenshot file was empty.");

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cameraGo);
            Object.DestroyImmediate(lightGo);
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(leftController);
            Object.DestroyImmediate(rightController);
        }

        private static GameObject CreateController(string name, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.12f, 0.06f, 0.22f);
            return go;
        }

        private static void CreateAnchor(string name, Vector3 position)
        {
            var anchor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            anchor.name = name;
            anchor.transform.position = position;
            anchor.transform.localScale = Vector3.one * 0.08f;
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
