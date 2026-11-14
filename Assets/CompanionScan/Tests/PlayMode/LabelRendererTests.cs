using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CompanionScan.Mapping;
using CompanionScan.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CompanionScan.Tests.PlayMode
{
    /// <summary>
    /// Each pin floats one label beside it, Space Setup and vision server pins alike, and the label
    /// stays with its pin: it moves when a newer detection of the object moves the pin and goes when
    /// the pin is cleared. The pipeline used to re-render every vision server pin after each capture,
    /// which left another copy of every label in the room each time.
    /// </summary>
    public class LabelRendererTests
    {
        private GameObject _root;
        private GameObject _labelPrefab;
        private SpatialPinManager _pins;
        private LabelRenderer _labels;
        private Transform _labelRoot;

        [SetUp]
        public void SetUp()
        {
            _labelPrefab = new GameObject("TestLabel");
            _labelPrefab.AddComponent<SpatialLabelView>();

            _root = new GameObject("CompanionScan");
            _pins = _root.AddComponent<SpatialPinManager>();
            _labels = _root.AddComponent<LabelRenderer>();
            _labelRoot = new GameObject("Labels").transform;
            SetPrivateField(_labels, "labelPrefab", _labelPrefab);
            SetPrivateField(_labels, "labelRoot", _labelRoot);
            _labels.Follow(_pins);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_labelRoot.gameObject);
            Object.DestroyImmediate(_labelPrefab);
        }

        [Test]
        public void EveryPin_GetsOneLabelBesideIt_SpaceSetupPinsToo()
        {
            var chair = CvPin("chair", new Vector3(-0.5f, 0.45f, 1.8f), trackId: 1);
            var lamp = CvPin("lamp", new Vector3(0.6f, 1.2f, 2.2f), trackId: 2);
            var table = new SpatialPin
            {
                label = "TABLE",
                semanticLabel = "TABLE",
                source = PinSource.MetaScene,
                anchorUuid = "anchor-1",
                position = new Vector3(0f, 0.7f, 1.2f)
            };

            _pins.AddPin(chair);
            _pins.AddPin(lamp);
            _pins.AddPin(table);

            Assert.AreEqual(3, _labels.PinLabelCount);
            Assert.AreEqual(3, _labelRoot.childCount);
            foreach (var pin in new[] { chair, lamp, table })
            {
                Assert.IsTrue(_labels.TryGetLabel(pin, out var label), pin.label + " has no label");
                Assert.AreEqual(pin.label, label.GetComponent<SpatialLabelView>().Text);
                AssertNear(pin.position + _labels.LabelOffset, label.transform.position);
                Assert.Greater(_labels.LabelOffset.y, 0f, "labels should float above their pins");
            }
        }

        [Test]
        public void ALabel_MovesWithItsPin_WhenANewerDetectionMovesThePin()
        {
            var chair = CvPin("chair", new Vector3(0f, 0.45f, 2f), trackId: 3);
            _pins.AddPin(chair);
            _pins.AddPin(CvPin("chair", new Vector3(0.1f, 0.45f, 2f), trackId: 3));

            Assert.AreEqual(1, _labels.PinLabelCount);
            Assert.IsTrue(_labels.TryGetLabel(_pins.Pins[0], out var label));
            AssertNear(_pins.Pins[0].position + _labels.LabelOffset, label.transform.position);
        }

        [Test]
        public void Labels_DoNotPileUp_OverRepeatedCaptures()
        {
            for (int capture = 0; capture < 5; capture++)
            {
                _pins.AddPin(CvPin("chair", new Vector3(0f, 0.45f, 2f), trackId: 1));
                _pins.AddPin(CvPin("lamp", new Vector3(1f, 1.2f, 2f), trackId: 2));
            }

            Assert.AreEqual(2, _pins.Pins.Count);
            Assert.AreEqual(2, _labels.PinLabelCount);
            Assert.AreEqual(2, _labelRoot.childCount);
        }

        [UnityTest]
        public IEnumerator ClearingPins_TakesTheirLabels()
        {
            _pins.AddPin(CvPin("chair", new Vector3(0f, 0.45f, 2f), trackId: 1));
            _pins.AddPin(CvPin("lamp", new Vector3(1f, 1.2f, 2f), trackId: 2));

            _pins.ClearPins();
            yield return null;

            Assert.AreEqual(0, _labels.PinLabelCount);
            Assert.AreEqual(0, _labelRoot.childCount);
        }

        [Test]
        public void Visible_HidesAndShowsTheLabels_AndNewLabelsFollowIt()
        {
            _pins.AddPin(CvPin("chair", new Vector3(0f, 0.45f, 2f), trackId: 1));

            _labels.Visible = false;
            _pins.AddPin(CvPin("lamp", new Vector3(1f, 1.2f, 2f), trackId: 2));

            foreach (Transform label in _labelRoot)
            {
                Assert.IsFalse(label.gameObject.activeSelf, "a hidden label is showing");
            }
            Assert.IsTrue(_root.activeSelf, "hiding the labels switched off the renderer's object");

            _labels.Visible = true;
            foreach (Transform label in _labelRoot)
            {
                Assert.IsTrue(label.gameObject.activeSelf, "a shown label is hidden");
            }
        }

        [Test]
        public void ThePipeline_ConnectsItsLabelRendererToItsPinManager()
        {
            var go = new GameObject("Pipeline");
            go.SetActive(false);
            var pins = go.AddComponent<SpatialPinManager>();
            var labels = go.AddComponent<LabelRenderer>();
            var pipeline = go.AddComponent<SpatialCapturePipeline>();
            SetPrivateField(pipeline, "preferArCameraOnQuest", false);
            SetPrivateField(pipeline, "pinManager", pins);
            SetPrivateField(pipeline, "labelRenderer", labels);

            go.SetActive(true);

            Assert.AreSame(pins, labels.PinManager);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void RenderLabels_StillPlacesFreeStandingLabels()
        {
            var points = new List<Vector3> { new Vector3(0f, 1f, 2f), new Vector3(1f, 1f, 2f) };
            _labels.RenderLabels(points, new List<string> { "cup", "book" });

            Assert.AreEqual(2, _labelRoot.childCount);
            var first = _labelRoot.GetChild(0);
            AssertNear(points[0] + _labels.LabelOffset, first.position);
            Assert.AreEqual("cup", first.GetComponent<SpatialLabelView>().Text);
        }

        [Test]
        public void ALabel_TurnsToFaceTheViewer_AndStaysUpright()
        {
            var label = Object.Instantiate(_labelPrefab).GetComponent<SpatialLabelView>();
            label.transform.position = new Vector3(2f, 1f, 2f);
            var viewer = new GameObject("Viewer").transform;
            viewer.position = new Vector3(0f, 1.6f, 0f);

            label.FaceViewer(viewer);

            // TextMesh Pro reads from its -Z side: +Z points away from the viewer, level.
            var away = (label.transform.position - viewer.position);
            away.y = 0f;
            Assert.That(Vector3.Angle(label.transform.forward, away), Is.LessThan(0.5f));
            Assert.That(Vector3.Angle(label.transform.up, Vector3.up), Is.LessThan(0.5f));

            Object.DestroyImmediate(viewer.gameObject);
            Object.DestroyImmediate(label.gameObject);
        }

        [Test]
        public void ALongLabel_IsShortened()
        {
            var label = Object.Instantiate(_labelPrefab).GetComponent<SpatialLabelView>();
            label.SetText(new string('x', 200));

            Assert.AreEqual(SpatialLabelView.MaxLength, label.Text.Length);
            StringAssert.EndsWith("...", label.Text);

            Object.DestroyImmediate(label.gameObject);
        }

        private static SpatialPin CvPin(string label, Vector3 position, int trackId)
        {
            return new SpatialPin
            {
                label = label,
                semanticLabel = label.ToUpperInvariant(),
                source = PinSource.CvServer,
                position = position,
                normal = Vector3.up,
                trackId = trackId
            };
        }

        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-4f), $"expected {expected}, got {actual}");
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} not found");
            field.SetValue(target, value);
        }
    }
}
