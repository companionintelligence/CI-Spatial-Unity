using System.Collections.Generic;
using System.Reflection;
using CompanionScan.Mapping;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Core.Tests
{
    /// <summary>
    /// The scene and raycast seams, tested without the Meta SDK: the conversion every scene backend
    /// shares, and the sources a <see cref="ViewportRaycaster"/> asks before its own raycasts.
    /// </summary>
    public class SceneSourceAndRaycastSourceTests
    {
        private static readonly Vector3 Far = new Vector3(1000f, 2000f, 3000f);

        private readonly List<Object> _created = new List<Object>();

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

        // ---- SceneSnapshotPins ----

        [Test]
        public void Convert_OfNothing_IsAnEmptyList()
        {
            Assert.IsEmpty(SceneSnapshotPins.Convert(null));
            Assert.IsEmpty(SceneSnapshotPins.Convert(new MetaSceneSnapshot { anchors = null }));
            Assert.IsEmpty(SceneSnapshotPins.Convert(new MetaSceneSnapshot()));
        }

        [Test]
        public void Convert_MakesOnePinPerAnchor_NamedByTheFirstLabel_WithAScoreOfOne()
        {
            var rotation = Quaternion.Euler(0f, 90f, 0f);
            var snapshot = new MetaSceneSnapshot
            {
                timestamp = 12.5,
                anchors =
                {
                    new MetaSceneAnchorSnapshot
                    {
                        uuid = "floor-1", labels = { "FLOOR", "OTHER" }, position = new Vector3(0f, 0f, 1f), rotation = rotation,
                        hasPlane = true, planeSize = new Vector3(4f, 3f, 0f)
                    },
                    new MetaSceneAnchorSnapshot
                    {
                        uuid = "box-1", labels = { "STORAGE" }, position = new Vector3(1f, 0.5f, 2f),
                        hasVolume = true, volumeSize = new Vector3(0.5f, 1f, 0.5f), rotation = Quaternion.identity
                    },
                    new MetaSceneAnchorSnapshot { uuid = "nameless", labels = { }, rotation = Quaternion.identity }
                }
            };

            var pins = SceneSnapshotPins.Convert(snapshot);

            Assert.AreEqual(3, pins.Count);
            Assert.AreEqual("FLOOR", pins[0].label);
            Assert.AreEqual("FLOOR", pins[0].semanticLabel);
            Assert.AreEqual(1f, pins[0].score);
            Assert.AreEqual(PinSource.MetaScene, pins[0].source);
            Assert.AreEqual("floor-1", pins[0].anchorUuid);
            Assert.AreEqual(12.5, pins[0].timestamp);
            Assert.AreEqual("meta_scene_understanding", pins[0].sourcePrompt);
            Assert.That(Vector3.Distance(rotation * Vector3.up, pins[0].normal), Is.LessThan(1e-5f), "a plane's normal is its rotation's up");
            Assert.AreEqual(new Vector3(4f, 3f, 0f), pins[0].planeSize);
            Assert.AreEqual(Vector3.zero, pins[0].volumeSize);
            Assert.AreEqual(new Vector3(0.5f, 1f, 0.5f), pins[1].volumeSize);
            Assert.AreEqual(Vector3.up, pins[1].normal, "an anchor without a plane faces up");
            Assert.AreEqual("OTHER", pins[2].label, "no label at all is OTHER");
        }

        // ---- IRaycastSource on ViewportRaycaster ----

        [Test]
        public void ASourceThatHits_AnswersTheViewportRaycast_WithItsPointNormalAndType()
        {
            var raycaster = NewRaycaster(NewCamera(Far, Quaternion.identity));
            raycaster.AddSource(new FakeSource(new RaycastSample(new Vector3(1f, 2f, 3f), Vector3.left, RaycastHitType.Environment)));

            var hit = raycaster.TryRaycastViewport(new Vector2(0.5f, 0.5f), out var point, out var normal, out var hitType);

            Assert.IsTrue(hit);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), point);
            Assert.AreEqual(Vector3.left, normal);
            Assert.AreEqual(RaycastHitType.Environment, hitType);
        }

        [Test]
        public void ASourceThatHits_AnswersTheCaptureRaycast_FromTheRayBuiltOutOfThePoseAndIntrinsics()
        {
            var raycaster = NewRaycaster(NewCamera(Far, Quaternion.identity));
            var source = new FakeSource(new RaycastSample(Vector3.one, Vector3.up, RaycastHitType.Environment));
            raycaster.AddSource(source);
            var intrinsics = Matrix4x4.zero;
            intrinsics.m00 = 500f;
            intrinsics.m11 = 500f;
            intrinsics.m02 = 320f;
            intrinsics.m12 = 240f;
            var frame = new CaptureFrame
            {
                imageWidth = 640, imageHeight = 480,
                cameraPose = new Pose(new Vector3(0f, 1.5f, 0f), Quaternion.Euler(0f, 90f, 0f)),
                cameraIntrinsics = intrinsics
            };

            var hit = raycaster.TryRaycastCapture(frame, new Vector2(420f, 240f), false, out var point, out _, out var hitType);

            Assert.IsTrue(hit);
            Assert.AreEqual(Vector3.one, point);
            Assert.AreEqual(RaycastHitType.Environment, hitType);
            Assert.AreEqual(1, source.Calls.Count);
            Assert.AreEqual(new Vector3(0f, 1.5f, 0f), source.Calls[0].origin);
            // 100 pixels right of the principal point with a 500 pixel focal length is atan(0.2) to the right; the camera looks along +x.
            var expected = Quaternion.Euler(0f, 90f, 0f) * new Vector3(0.2f, 0f, 1f).normalized;
            Assert.That(Vector3.Distance(expected, source.Calls[0].direction), Is.LessThan(1e-4f));
        }

        [Test]
        public void SourcesAreAskedInTheOrderTheyWereAdded_AndTheFirstHitWins()
        {
            var raycaster = NewRaycaster(NewCamera(Far, Quaternion.identity));
            var misses = new FakeSource(null);
            var first = new FakeSource(new RaycastSample(Vector3.right, Vector3.up, RaycastHitType.MetaPlane));
            var second = new FakeSource(new RaycastSample(Vector3.left, Vector3.up, RaycastHitType.Environment));
            raycaster.AddSource(misses);
            raycaster.AddSource(first);
            raycaster.AddSource(second);

            raycaster.TryRaycastViewport(new Vector2(0.5f, 0.5f), out var point, out _, out var hitType);

            Assert.AreEqual(Vector3.right, point);
            Assert.AreEqual(RaycastHitType.MetaPlane, hitType);
            Assert.AreEqual(1, misses.Calls.Count);
            Assert.AreEqual(1, first.Calls.Count);
            Assert.AreEqual(0, second.Calls.Count, "a source after the first hit is not asked");
        }

        [Test]
        public void WhenNoSourceHits_TheRaycasterFallsBackToItsOwnRaycasts()
        {
            var camera = NewCamera(Far, Quaternion.identity);
            var raycaster = NewRaycaster(camera);
            raycaster.AddSource(new FakeSource(null));
            var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            var hit = raycaster.TryRaycastViewport(new Vector2(0.5f, 0.5f), out var point, out _, out var hitType);

            Assert.IsFalse(hit, "nothing in the scene is that far away");
            Assert.That(Vector3.Distance(ray.origin + ray.direction * 2f, point), Is.LessThan(1e-3f), "the fallback point");
            Assert.AreEqual(RaycastHitType.Fallback, hitType);
        }

        [Test]
        public void AddSource_IgnoresNullAndDuplicates_AndRemoveSourceTakesOneOut()
        {
            var raycaster = NewRaycaster(NewCamera(Far, Quaternion.identity));
            var source = new FakeSource(new RaycastSample(Vector3.one, Vector3.up, RaycastHitType.Environment));

            raycaster.AddSource(null);
            raycaster.AddSource(source);
            raycaster.AddSource(source);

            Assert.AreEqual(1, raycaster.SourceCount);
            Assert.IsTrue(raycaster.RemoveSource(source));
            Assert.IsFalse(raycaster.RemoveSource(source), "already out");
            Assert.IsFalse(raycaster.RemoveSource(null));
            Assert.AreEqual(0, raycaster.SourceCount);
            Assert.IsFalse(raycaster.TryRaycastViewport(new Vector2(0.5f, 0.5f), out _, out _, out _), "a removed source no longer answers");
            Assert.AreEqual(0, source.Calls.Count);
        }

        [Test]
        public void TheSourceIsGivenTheRaycastersMaximumDistance()
        {
            var raycaster = NewRaycaster(NewCamera(Far, Quaternion.identity));
            var source = new FakeSource(null);
            raycaster.AddSource(source);

            raycaster.TryRaycastViewport(new Vector2(0.5f, 0.5f), out _, out _, out _);

            Assert.AreEqual(10f, source.LastMaxDistance, "the raycaster's default");
        }

        private ViewportRaycaster NewRaycaster(Camera camera)
        {
            var go = new GameObject("ViewportRaycaster");
            _created.Add(go);
            var raycaster = go.AddComponent<ViewportRaycaster>();
            var field = typeof(ViewportRaycaster).GetField("targetCamera", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "ViewportRaycaster.targetCamera not found");
            field.SetValue(raycaster, camera);
            return raycaster;
        }

        private Camera NewCamera(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("TargetCamera");
            _created.Add(go);
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.aspect = 2f;
            go.transform.SetPositionAndRotation(position, rotation);
            return camera;
        }

        private sealed class FakeSource : IRaycastSource
        {
            private readonly RaycastSample? _hit;

            public FakeSource(RaycastSample? hit)
            {
                _hit = hit;
            }

            public List<Ray> Calls { get; } = new List<Ray>();
            public float LastMaxDistance { get; private set; }

            public bool TryRaycast(Ray ray, float maxDistance, out RaycastSample hit)
            {
                Calls.Add(ray);
                LastMaxDistance = maxDistance;
                hit = _hit ?? default;
                return _hit.HasValue;
            }
        }
    }
}
