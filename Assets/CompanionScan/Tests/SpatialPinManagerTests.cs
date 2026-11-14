using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CompanionScan.Mapping;

namespace CompanionScan.Tests
{
    public class SpatialPinManagerTests
    {
        [Test]
        public void AddPin_KeepsTheEarlierCapturesPins_ByDefault()
        {
            // Labels persist across captures (README). The scene used to drop every pin more than
            // 6 s older than the newest, so each capture removed the ones before it.
            var go = new GameObject("PinManager");
            var manager = go.AddComponent<SpatialPinManager>();

            manager.AddPin(CvPin("chair", new Vector3(-1f, 0f, 2f), 0.0, trackId: 1));
            manager.AddPin(CvPin("lamp", new Vector3(1f, 0f, 2f), 30.0, trackId: 2));
            manager.AddPin(SpaceSetupPin("TABLE", "anchor-1", new Vector3(0f, 0.7f, 1f), 600.0));

            Assert.That(manager.Pins.Select(p => p.label), Is.EqualTo(new[] { "chair", "lamp", "TABLE" }));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AddPin_ANewerDetectionOfTheSameObjectReplacesItsPin()
        {
            var go = new GameObject("PinManager");
            var manager = go.AddComponent<SpatialPinManager>();

            for (int capture = 0; capture < 5; capture++)
            {
                manager.AddPin(CvPin("chair", new Vector3(0f, 0f, 2f + 0.01f * capture), capture * 20.0, trackId: 7));
            }

            Assert.That(manager.Pins.Count, Is.EqualTo(1));
            Assert.That(manager.Pins[0].timestamp, Is.EqualTo(80.0));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AddPin_ANewerSpaceSetupReadingOfTheSameAnchorReplacesItsPin()
        {
            var go = new GameObject("PinManager");
            var manager = go.AddComponent<SpatialPinManager>();

            manager.AddPin(SpaceSetupPin("TABLE", "anchor-1", new Vector3(0f, 0.7f, 1f), 1.0));
            var moved = SpaceSetupPin("TABLE", "anchor-1", new Vector3(0.6f, 0.7f, 1f), 50.0);
            moved.volumeSize = new Vector3(1.2f, 0.7f, 0.6f);
            manager.AddPin(moved);
            // Another table next to it is another object, even within the deduplication distance.
            manager.AddPin(SpaceSetupPin("TABLE", "anchor-2", new Vector3(0.65f, 0.7f, 1f), 50.0));

            Assert.That(manager.Pins.Count, Is.EqualTo(2));
            Assert.That(manager.Pins[0], Is.SameAs(moved));
            Assert.That(manager.Pins[1].anchorUuid, Is.EqualTo("anchor-2"));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AddPin_KeepsDifferentObjectsWithoutAMetaLabelApart()
        {
            // A cup next to a book: both map to OTHER, within the deduplication distance.
            var go = new GameObject("PinManager");
            var manager = go.AddComponent<SpatialPinManager>();

            var cup = CvPin("cup", new Vector3(0f, 0.8f, 1.5f), 0.0, trackId: 1);
            cup.semanticLabel = "OTHER";
            var book = CvPin("book", new Vector3(0.1f, 0.8f, 1.5f), 0.0, trackId: 2);
            book.semanticLabel = "OTHER";
            manager.AddPin(cup);
            manager.AddPin(book);

            Assert.That(manager.Pins.Select(p => p.label), Is.EqualTo(new[] { "cup", "book" }));

            Object.DestroyImmediate(go);
        }

        [UnityTest]
        public IEnumerator ClearPins_RemovesThePinsAndTheirObjects()
        {
            var go = new GameObject("PinManager");
            var manager = go.AddComponent<SpatialPinManager>();
            var prefab = new GameObject("TestPin");
            SetPrivateField(manager, "pinPrefab", prefab);

            manager.AddPin(CvPin("chair", new Vector3(-1f, 0f, 2f), 0.0, trackId: 1));
            manager.AddPin(SpaceSetupPin("TABLE", "anchor-1", new Vector3(0f, 0.7f, 1f), 0.0));
            Assert.That(manager.PinRoot.childCount, Is.EqualTo(2));

            manager.ClearPins();
            yield return null;

            Assert.That(manager.Pins, Is.Empty);
            Assert.That(manager.PinObjects, Is.Empty);
            Assert.That(manager.PinRoot.childCount, Is.EqualTo(0));

            Object.Destroy(prefab);
            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator ClearPins_OfOneSource_KeepsEachRemainingPinWithItsObject()
        {
            var go = new GameObject("PinManager");
            var manager = go.AddComponent<SpatialPinManager>();

            // Added while no prefab was set: kept and exported, with no object.
            manager.AddPin(CvPin("chair", new Vector3(-1f, 0f, 2f), 0.0, trackId: 1));
            var prefab = new GameObject("TestPin");
            SetPrivateField(manager, "pinPrefab", prefab);
            var table = SpaceSetupPin("TABLE", "anchor-1", new Vector3(0f, 0.7f, 1f), 0.0);
            manager.AddPin(table);

            manager.ClearPins(PinSource.CvServer);
            yield return null;

            Assert.That(manager.Pins.Single(), Is.SameAs(table));
            Assert.That(manager.PinObjects.Count, Is.EqualTo(1), "pins and their objects fell out of step");
            Assert.That(manager.PinObjects[0], Is.Not.Null);
            Assert.That(Vector3.Distance(manager.PinObjects[0].transform.position, table.position), Is.LessThan(1e-4f));

            Object.Destroy(prefab);
            Object.Destroy(go);
        }

        [Test]
        public void PinsChanged_IsRaisedForEveryChange()
        {
            var go = new GameObject("PinManager");
            var manager = go.AddComponent<SpatialPinManager>();
            int changes = 0;
            manager.PinsChanged += () => changes++;

            manager.AddPin(CvPin("chair", new Vector3(-1f, 0f, 2f), 0.0, trackId: 1));
            manager.AddPin(CvPin("chair", new Vector3(-1f, 0f, 2.05f), 5.0, trackId: 1));
            manager.ClearPins(PinSource.CvServer);
            manager.ClearPins();

            Assert.That(changes, Is.EqualTo(4));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PinsVisible_HidesAndShowsThePinObjects_AndNewPinsFollowIt()
        {
            var go = new GameObject("PinManager");
            var manager = go.AddComponent<SpatialPinManager>();
            var prefab = new GameObject("TestPin");
            SetPrivateField(manager, "pinPrefab", prefab);

            manager.AddPin(CvPin("chair", new Vector3(-1f, 0f, 2f), 0.0, trackId: 1));
            manager.PinsVisible = false;
            manager.AddPin(CvPin("lamp", new Vector3(1f, 0f, 2f), 1.0, trackId: 2));

            Assert.That(manager.PinObjects.All(o => !o.activeSelf), "hidden pins are showing");
            Assert.That(go.activeSelf, "hiding the pins switched off the pin manager's object");

            manager.PinsVisible = true;
            Assert.That(manager.PinObjects.All(o => o.activeSelf), "shown pins are hidden");

            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(go);
        }

        private static SpatialPin CvPin(string label, Vector3 position, double timestamp, int trackId)
        {
            return new SpatialPin
            {
                label = label,
                semanticLabel = label.ToUpperInvariant(),
                source = PinSource.CvServer,
                position = position,
                normal = Vector3.up,
                timestamp = timestamp,
                trackId = trackId
            };
        }

        private static SpatialPin SpaceSetupPin(string label, string anchorUuid, Vector3 position, double timestamp)
        {
            return new SpatialPin
            {
                label = label,
                semanticLabel = label,
                source = PinSource.MetaScene,
                anchorUuid = anchorUuid,
                position = position,
                normal = Vector3.up,
                score = 1f,
                timestamp = timestamp
            };
        }

        [Test]
        public void AddPin_MergesByTrackId()
        {
            var go = new GameObject("PinManager");
            var manager = go.AddComponent<SpatialPinManager>();
            SetPrivateField(manager, "enablePinLifecycle", false);
            SetPrivateField(manager, "preferTrackIds", true);
            SetPrivateField(manager, "enableDeduplication", false);

            var first = new SpatialPin
            {
                label = "Chair",
                semanticLabel = "chair",
                position = Vector3.zero,
                normal = Vector3.up,
                timestamp = 1.0,
                trackId = 5
            };

            var second = new SpatialPin
            {
                label = "Chair",
                semanticLabel = "chair",
                position = new Vector3(2f, 0f, 0f),
                normal = Vector3.up,
                timestamp = 2.0,
                trackId = 5
            };

            manager.AddPin(first);
            manager.AddPin(second);

            Assert.That(manager.Pins.Count, Is.EqualTo(1));
            Assert.That(manager.Pins[0].position.x, Is.EqualTo(1f).Within(0.0001f));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AddPin_PrunesStalePins()
        {
            var go = new GameObject("PinManager");
            var manager = go.AddComponent<SpatialPinManager>();
            SetPrivateField(manager, "enablePinLifecycle", true);
            SetPrivateField(manager, "pinMaxAgeSeconds", 1f);
            SetPrivateField(manager, "preferTrackIds", false);
            SetPrivateField(manager, "enableDeduplication", false);

            var oldPin = new SpatialPin
            {
                label = "Lamp",
                semanticLabel = "lamp",
                position = Vector3.zero,
                normal = Vector3.up,
                timestamp = 0.0
            };

            var newPin = new SpatialPin
            {
                label = "Lamp",
                semanticLabel = "lamp",
                position = new Vector3(0f, 0f, 1f),
                normal = Vector3.up,
                timestamp = 10.0
            };

            manager.AddPin(oldPin);
            manager.AddPin(newPin);

            Assert.That(manager.Pins.Count, Is.EqualTo(1));
            Assert.That(manager.Pins[0].position.z, Is.EqualTo(1f).Within(0.0001f));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AddPin_DeduplicatesBySemanticLabel()
        {
            var go = new GameObject("PinManager");
            var manager = go.AddComponent<SpatialPinManager>();
            SetPrivateField(manager, "enablePinLifecycle", false);
            SetPrivateField(manager, "preferTrackIds", false);
            SetPrivateField(manager, "enableDeduplication", true);
            SetPrivateField(manager, "dedupDistance", 2f);

            var first = new SpatialPin
            {
                label = "Chair",
                semanticLabel = "chair",
                position = Vector3.zero,
                normal = Vector3.up,
                timestamp = 1.0
            };

            var second = new SpatialPin
            {
                label = "Chair",
                semanticLabel = "CHAIR",
                position = new Vector3(1f, 0f, 0f),
                normal = Vector3.up,
                timestamp = 2.0
            };

            manager.AddPin(first);
            manager.AddPin(second);

            Assert.That(manager.Pins.Count, Is.EqualTo(1));
            Assert.That(manager.Pins[0].position.x, Is.EqualTo(0.5f).Within(0.0001f));

            Object.DestroyImmediate(go);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
            field.SetValue(target, value);
        }
    }
}
