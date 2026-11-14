using System.Reflection;
using CompanionScan.Core;
using CompanionScan.Mapping;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Tests.PlayMode
{
    /// <summary>
    /// Each vision server detection is pinned where that object was found. Positions used to be
    /// smoothed by Meta label alone, and every class without a Meta label is "OTHER", so a chair,
    /// a cup and a book pulled each other's pins (and labels) toward one another.
    /// </summary>
    public class CvPinPlacementTests
    {
        private GameObject _cameraGo;
        private Camera _camera;
        private GameObject _captureGo;
        private SpatialCapturePipeline _pipeline;
        private SpatialPinManager _pins;

        [SetUp]
        public void SetUp()
        {
            // Far from the origin, where other tests leave nothing a ray could hit: every pin lands
            // at the raycaster's fallback distance along its ray.
            _cameraGo = new GameObject("PlacementCamera");
            _camera = _cameraGo.AddComponent<Camera>();
            _camera.fieldOfView = 60f;
            _camera.aspect = 1.6f;
            _cameraGo.transform.position = new Vector3(500f, 50f, 500f);

            _captureGo = new GameObject("CompanionScan");
            _captureGo.SetActive(false);
            _pipeline = _captureGo.AddComponent<SpatialCapturePipeline>();
            var raycaster = _captureGo.AddComponent<ViewportRaycaster>();
            _pins = _captureGo.AddComponent<SpatialPinManager>();
            SetPrivateField(_pipeline, "preferArCameraOnQuest", false);
            SetPrivateField(_pipeline, "raycaster", raycaster);
            SetPrivateField(_pipeline, "pinManager", _pins);
            SetPrivateField(_pipeline, "snapshotCamera", _camera);
            SetPrivateField(raycaster, "targetCamera", _camera);
            _captureGo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_captureGo);
            Object.DestroyImmediate(_cameraGo);
        }

        [Test]
        public void ObjectsWithoutAMetaLabel_ArePinnedWhereEachWasDetected()
        {
            // chair, cup and book all map to OTHER.
            var detection = Detection(("chair", 0.2f), ("cup", 0.5f), ("book", 0.8f));

            AddPins(detection);

            Assert.AreEqual(3, _pins.Pins.Count);
            for (int i = 0; i < 3; i++)
            {
                var expected = FallbackPoint(detection.bboxes[i]);
                Assert.That(Vector3.Distance(expected, _pins.Pins[i].position), Is.LessThan(1e-3f),
                    $"{_pins.Pins[i].label} was moved toward another object");
            }
        }

        [Test]
        public void TwoObjectsInOneCapture_KeepTwoPins()
        {
            // Two cups about 25 cm apart: closer than the tracking distance, further than deduplication.
            var detection = Detection(("cup", 0.46f), ("cup", 0.53f));
            float apart = Vector3.Distance(FallbackPoint(detection.bboxes[0]), FallbackPoint(detection.bboxes[1]));
            Assert.That(apart, Is.InRange(0.16f, 0.39f), "the test's cups are not where it needs them");

            AddPins(detection);

            Assert.AreEqual(2, _pins.Pins.Count, "two cups in one capture became one pin");
        }

        [Test]
        public void TheSameObjectInTheNextCapture_UpdatesItsPin()
        {
            AddPins(Detection(("lamp", 0.3f)));
            AddPins(Detection(("lamp", 0.31f)));

            Assert.AreEqual(1, _pins.Pins.Count);
        }

        private void AddPins(DetectionResult detection)
        {
            var method = typeof(SpatialCapturePipeline).GetMethod("AddCvPinsFromDetections",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(_pipeline, new object[] { detection, null });
        }

        private static DetectionResult Detection(params (string label, float u)[] objects)
        {
            var detection = new DetectionResult();
            foreach (var (label, u) in objects)
            {
                detection.bboxes.Add(new[] { u - 0.03f, 0.45f, u + 0.03f, 0.55f });
                detection.labels.Add(label);
                detection.scores.Add(0.8f);
            }

            return detection;
        }

        private Vector3 FallbackPoint(float[] bbox)
        {
            float u = (bbox[0] + bbox[2]) * 0.5f;
            float v = 1f - (bbox[1] + bbox[3]) * 0.5f;
            var ray = _camera.ViewportPointToRay(new Vector3(u, v, 0f));
            return ray.origin + ray.direction * 2f; // ViewportRaycaster's default fallback distance
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} not found");
            field.SetValue(target, value);
        }
    }
}
