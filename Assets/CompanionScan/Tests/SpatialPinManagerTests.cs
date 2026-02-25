using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CompanionScan.Mapping;

namespace CompanionScan.Tests
{
    public class SpatialPinManagerTests
    {
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
