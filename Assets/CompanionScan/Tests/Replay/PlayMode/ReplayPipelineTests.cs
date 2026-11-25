using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CompanionScan.Core;
using CompanionScan.Mapping;
using CompanionScan.Replay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CompanionScan.Replay.Tests
{
    /// <summary>
    /// The whole capture pipeline with no headset: a recorded room supplies the camera frames (with their
    /// poses and intrinsics), the Space Setup read and the raycasts, and a server on a loopback port
    /// plays the CV server. What comes out is checked against geometry worked out by hand. The room is
    /// the synthetic one: a 4 x 6 m box, with the camera at (0, 1.5, 0) and a 500 pixel focal length.
    /// </summary>
    public class ReplayPipelineTests
    {
        private const string Token = "replay-test-token";

        private PrefsGuard _prefs;
        private MockCvServer _server;
        private ServerConfig _config;
        private GameObject _host;
        private GameObject _cameraGo;
        private SpatialCapturePipeline _pipeline;
        private SpatialPinManager _pins;
        private SnapshotFrameStore _snapshots;
        private ReplayRig _rig;

        [SetUp]
        public void SetUp()
        {
            // The app reads its server and token from player prefs first, so the test sets them there (and
            // puts back whatever this machine had).
            _prefs = new PrefsGuard(
                ServerConfig.PlayerPrefsBaseUrlKey, ServerConfig.PlayerPrefsApiTokenKey,
                CaptureConsent.PlayerPrefsVersionKey, CaptureConsent.PlayerPrefsAgreedAtKey, CaptureConsent.PlayerPrefsServerUrlKey);
            _server = new MockCvServer(Token);
            PlayerPrefs.SetString(ServerConfig.PlayerPrefsBaseUrlKey, _server.Url);
            PlayerPrefs.SetString(ServerConfig.PlayerPrefsApiTokenKey, Token);

            _config = ScriptableObject.CreateInstance<ServerConfig>();
            _config.baseUrl = _server.Url;
            _config.retryCount = 0;
            _config.requestTimeoutSeconds = 10;
            CaptureConsent.Grant(_server.Url);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
            }

            if (_cameraGo != null)
            {
                Object.DestroyImmediate(_cameraGo);
            }

            if (_config != null)
            {
                Object.DestroyImmediate(_config);
            }

            _server?.Dispose();
            _prefs?.Dispose();
        }

        // ---- the image goes to the server, and what the server finds is pinned where the recorded camera saw it ----

        [UnityTest]
        public IEnumerator AChairDetectedRightOfTheMiddle_IsPinnedOnTheFarWallWhereThatPixelLooks()
        {
            // The box is centred 100 pixels right of the principal point: 0.2 of the distance to the right, 0.6 m at the wall 3 m ahead.
            _server.Detections = _ => new[] { DetectionReply.InPixels("chair", 0.9f, 380f, 200f, 460f, 280f) };
            _server.FlorenceLabels = new[] { "chair" };
            var recording = SyntheticRoom.Build(0f);
            BuildScene(recording);

            _pipeline.CaptureAndPin();
            yield return WaitForTheCapture();

            Assert.AreEqual(1, _pins.CvPins.Count);
            var pin = _pins.CvPins[0];
            AssertNear(new Vector3(0.6f, 1.5f, 3f), pin.position, "the pin is not where the recorded camera's pixel meets the wall");
            Assert.AreEqual(RaycastHitType.Environment, pin.hitType);
            Assert.AreEqual(PinSource.CvServer, pin.source);
            Assert.AreEqual("chair", pin.label);
            Assert.That(pin.score, Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That(pin.placementConfidence, Is.EqualTo(0.9f * 0.9f).Within(1e-3f), "the model score weighted by an environment hit");
            Assert.AreEqual(0, pin.captureFrameIndex);
            Assert.AreEqual(_snapshots.Frames[0].id, pin.captureId, "the pin names the snapshot it was found in");
            Assert.IsTrue(_pipeline.LastCaptureHadIntrinsics);
            Assert.AreEqual(0, _pipeline.LastFallbackHitCount);
        }

        [UnityTest]
        public IEnumerator TheServerGetsTheRecordedFrame_TheBearerToken_AndFlorencesLabelsAsGroundingDinosPrompt()
        {
            _server.Detections = _ => new[] { DetectionReply.InPixels("chair", 0.9f, 300f, 220f, 340f, 260f) };
            _server.FlorenceLabels = new[] { "chair", "lamp" };
            var recording = SyntheticRoom.Build(0f);
            BuildScene(recording);

            _pipeline.CaptureAndPin();
            yield return WaitForTheCapture();

            // Caption, object detection, detection, segmentation: the pipeline's four steps, in order.
            CollectionAssert.AreEqual(new[] { "/v1/florence", "/v1/florence", "/v1/detect", "/v1/segment" }, _server.Paths);
            var requests = _server.Requests;
            Assert.IsTrue(requests.All(request => request.Authorization == "Bearer " + Token), "every route needs the token");
            Assert.AreEqual("<MORE_DETAILED_CAPTION>", (string)requests[0].Json["task"]);
            Assert.AreEqual("<OD>", (string)requests[1].Json["task"]);
            Assert.AreEqual("chair . lamp .", (string)requests[2].Json["prompt"], "Florence's labels became GroundingDINO's prompt");
            CollectionAssert.AreEqual(recording.frames[0].jpeg, Convert.FromBase64String((string)requests[2].Json["image"]),
                "what was sent is the recorded frame, byte for byte");
            Assert.AreEqual("an empty room", _pipeline.LastCaption);
        }

        [UnityTest]
        public IEnumerator EachCaptureTakesTheNextRecordedFrame_WithItsOwnPose()
        {
            // Frame 0 looks along +z (the wall 3 m ahead), frame 1 along +x (the wall 2 m ahead).
            var chairInTheMiddle = DetectionReply.InPixels("chair", 0.9f, 300f, 220f, 340f, 260f);
            var lampInTheMiddle = DetectionReply.InPixels("lamp", 0.8f, 300f, 220f, 340f, 260f);
            _server.Detections = call => new[] { call == 0 ? chairInTheMiddle : lampInTheMiddle };
            var recording = SyntheticRoom.Build(0f, 90f);
            BuildScene(recording);

            _pipeline.CaptureAndPin();
            yield return WaitForTheCapture();
            _pipeline.CaptureAndPin();
            yield return WaitForTheCapture();

            Assert.AreEqual(2, _pins.CvPins.Count);
            var chair = _pins.CvPins.First(pin => pin.label == "chair");
            var lamp = _pins.CvPins.First(pin => pin.label == "lamp");
            AssertNear(new Vector3(0f, 1.5f, 3f), chair.position, "the chair, in frame 0");
            AssertNear(new Vector3(2f, 1.5f, 0f), lamp.position, "the lamp, in frame 1");
            Assert.AreEqual(0, chair.captureFrameIndex);
            Assert.AreEqual(1, lamp.captureFrameIndex);
            Assert.AreEqual(2, _snapshots.Frames.Count);
            Assert.AreEqual(2, _rig.Camera.NextFrameIndex);
        }

        // An object seen again thirty seconds later (a second sweep of the room) is the same object: one pin, at the
        // smoothed place. The tracker used to forget an object after 2.5 seconds, so it was pinned twice when the second
        // sighting was further than the pin manager's deduplication distance (0.15 m) from the first.
        [UnityTest]
        public IEnumerator TheSameObjectSeenAgainThirtySecondsLater_IsOnePin_AtItsSmoothedPlace()
        {
            // 42 pixels is 0.25 m at the wall 3 m ahead: past the deduplication distance, within the tracker's 0.4 m.
            var first = DetectionReply.InPixels("chair", 0.9f, 300f, 220f, 340f, 260f);
            var second = DetectionReply.InPixels("chair", 0.9f, 342f, 220f, 382f, 260f);
            _server.Detections = call => new[] { call == 0 ? first : second };
            var recording = SyntheticRoom.Build(0f, 0f);
            recording.frames[1].timestamp = 40.0;
            BuildScene(recording);

            _pipeline.CaptureAndPin();
            yield return WaitForTheCapture();
            var firstTrack = _pins.CvPins[0].trackId;
            _pipeline.CaptureAndPin();
            yield return WaitForTheCapture();

            Assert.AreEqual(1, _pins.CvPins.Count, "the chair was pinned twice");
            Assert.Greater(firstTrack, 0);
            Assert.AreEqual(firstTrack, _pins.CvPins[0].trackId, "a new track for the same chair");
            // The first sighting was at x = 0 and the second at 0.252: the pin moved towards the second, smoothed, not onto it.
            // (How far depends on the smoothing, which the tracker and the pin manager both apply.)
            var position = _pins.CvPins[0].position;
            Assert.That(position.x, Is.GreaterThan(0.01f).And.LessThan(0.252f), "the pin did not move towards the second sighting, or jumped onto it");
            Assert.That(position.y, Is.EqualTo(1.5f).Within(2e-3f));
            Assert.That(position.z, Is.EqualTo(3f).Within(2e-3f));
        }

        // Two objects in one capture are two objects, however close.
        [UnityTest]
        public IEnumerator TwoChairsInOneCapture_AreTwoPins()
        {
            _server.Detections = _ => new[]
            {
                DetectionReply.InPixels("chair", 0.9f, 300f, 220f, 340f, 260f),
                DetectionReply.InPixels("chair", 0.8f, 342f, 220f, 382f, 260f)
            };
            BuildScene(SyntheticRoom.Build(0f));

            _pipeline.CaptureAndPin();
            yield return WaitForTheCapture();

            Assert.AreEqual(2, _pins.CvPins.Count);
            Assert.AreNotEqual(_pins.CvPins[0].trackId, _pins.CvPins[1].trackId);
        }

        // A recorded pose is the pose at capture, even when it is the world origin: it is not swapped for the scene camera's.
        [UnityTest]
        public IEnumerator ARecordedPoseAtTheWorldOrigin_IsKept_WhateverTheSceneCameraSays()
        {
            _server.Detections = _ => new[] { DetectionReply.InPixels("chair", 0.9f, 300f, 220f, 340f, 260f) };
            var recording = SyntheticRoom.Build(0f);
            recording.frames[0].pose = new Pose(Vector3.zero, Quaternion.identity);
            BuildScene(recording, sceneCameraAt: new Vector3(1f, 1.7f, -2f));

            _pipeline.CaptureAndPin();
            yield return WaitForTheCapture();

            Assert.AreEqual(1, _pins.CvPins.Count);
            AssertNear(new Vector3(0f, 0f, 3f), _pins.CvPins[0].position, "the ray started somewhere other than the recorded pose");
            Assert.AreEqual(Vector3.zero, _snapshots.Frames[0].cameraPosition);
        }

        // The snapshot's frustum is the recorded camera's view, not the scene camera's (which is elsewhere).
        [UnityTest]
        public IEnumerator TheSnapshotsFrustum_IsTheRecordedCamerasView()
        {
            _server.Detections = _ => Array.Empty<DetectionReply>();
            var recording = SyntheticRoom.Build(0f);
            BuildScene(recording, sceneCameraAt: new Vector3(40f, 41f, 42f));

            _pipeline.CaptureAndPin();
            yield return WaitForTheCapture();

            // One metre ahead of a 640 x 480 camera with a 500 pixel focal length: 1.28 m wide and 0.96 m high.
            var corners = _snapshots.Frames[0].frustumCorners;
            Assert.AreEqual(4, corners.Length);
            AssertNear(new Vector3(-0.64f, 1.5f - 0.48f, 1f), corners[0], "bottom left");
            AssertNear(new Vector3(-0.64f, 1.5f + 0.48f, 1f), corners[1], "top left");
            AssertNear(new Vector3(0.64f, 1.5f + 0.48f, 1f), corners[2], "top right");
            AssertNear(new Vector3(0.64f, 1.5f - 0.48f, 1f), corners[3], "bottom right");
        }

        // ---- the Space Setup read ----

        [UnityTest]
        public IEnumerator TheSceneBackend_PinsTheRecordedRoom()
        {
            BuildScene(SyntheticRoom.Build(0f));

            _pipeline.CaptureMetaScene();
            yield return WaitForTheCapture();

            Assert.AreEqual(6, _pins.MetaScenePins.Count);
            CollectionAssert.AreEquivalent(new[] { "FLOOR", "CEILING", "WALL_FACE", "WALL_FACE", "WALL_FACE", "WALL_FACE" },
                _pins.MetaScenePins.Select(pin => pin.label).ToArray());
            Assert.AreEqual(6, _pipeline.LastMetaSnapshot.anchors.Count);
            Assert.AreEqual(0, _server.Requests.Count, "Space Setup is read on the device: nothing goes to the server");
        }

        [UnityTest]
        public IEnumerator CaptureAll_ReadsTheRoomAndFindsTheObjects()
        {
            _server.Detections = _ => new[] { DetectionReply.InPixels("lamp", 0.8f, 300f, 220f, 340f, 260f) };
            BuildScene(SyntheticRoom.Build(0f));

            _pipeline.CaptureAll();
            yield return WaitForTheCapture();

            Assert.AreEqual(6, _pins.MetaScenePins.Count);
            Assert.AreEqual(1, _pins.CvPins.Count);
        }

        // ---- consent ----

        [UnityTest]
        public IEnumerator WithoutTheAgreementToSendSnapshots_NothingIsCaptured_AndNothingIsSent()
        {
            _server.Detections = _ => new[] { DetectionReply.InPixels("chair", 0.9f, 300f, 220f, 340f, 260f) };
            BuildScene(SyntheticRoom.Build(0f));
            CaptureConsent.Withdraw();

            _pipeline.CaptureAndPin();
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.AreEqual(0, _server.Requests.Count, "an image went to the server without the user's agreement");
            Assert.AreEqual(0, _pins.Pins.Count);
            Assert.AreEqual(0, _rig.Camera.NextFrameIndex, "the camera was read before the agreement");
            Assert.AreEqual(0, _snapshots.Frames.Count);
        }

        // ---- a recording that has run out ----

        [UnityTest]
        public IEnumerator WhenTheRecordingHasRunOut_TheCaptureStops_WithNothingSent()
        {
            var recording = SyntheticRoom.Build(0f);
            BuildScene(recording);
            _rig.Camera.Advance();
            LogAssert.Expect(LogType.Warning, "[Pipeline-CV] Failed to capture frame from camera.");

            _pipeline.CaptureAndPin();
            yield return WaitForTheCapture();

            Assert.AreEqual(0, _server.Requests.Count);
            Assert.AreEqual(0, _pins.Pins.Count);
        }

        // ---- helpers ----

        private void BuildScene(ScanRecording recording, Vector3? sceneCameraAt = null)
        {
            if (sceneCameraAt.HasValue)
            {
                _cameraGo = new GameObject("SceneCamera");
                _cameraGo.AddComponent<Camera>();
                _cameraGo.transform.position = sceneCameraAt.Value;
            }

            _host = new GameObject("CompanionScan");
            _host.SetActive(false);
            _pipeline = _host.AddComponent<SpatialCapturePipeline>();
            var raycaster = _host.AddComponent<ViewportRaycaster>();
            _pins = _host.AddComponent<SpatialPinManager>();
            _snapshots = _host.AddComponent<SnapshotFrameStore>();
            SetPrivateField(_pipeline, "serverConfig", _config);
            SetPrivateField(_pipeline, "preferArCameraOnQuest", false);
            SetPrivateField(_pipeline, "raycaster", raycaster);
            SetPrivateField(_pipeline, "pinManager", _pins);
            SetPrivateField(_pipeline, "snapshotStore", _snapshots);
            if (_cameraGo != null)
            {
                SetPrivateField(_pipeline, "snapshotCamera", _cameraGo.GetComponent<Camera>());
                SetPrivateField(raycaster, "targetCamera", _cameraGo.GetComponent<Camera>());
            }

            _rig = ReplayRig.Attach(_host, recording, _pipeline, raycaster);
            _host.SetActive(true);
        }

        private IEnumerator WaitForTheCapture(float timeoutSeconds = 20f)
        {
            // CaptureAndPin is "async void": it is busy from its first line, and drops back when it is done.
            yield return null;
            float waited = 0f;
            while (_pipeline.IsBusy && waited < timeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsFalse(_pipeline.IsBusy, $"the capture was not done after {timeoutSeconds} s");
        }

        private static void AssertNear(Vector3 expected, Vector3 actual, string message = null)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(2e-3f), $"{message}: expected {expected} but was {actual}");
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} not found");
            field.SetValue(target, value);
        }

        /// <summary>Remembers player prefs keys and puts them back.</summary>
        private sealed class PrefsGuard : IDisposable
        {
            private readonly (string key, bool had, string value)[] _saved;
            private bool _restored;

            public PrefsGuard(params string[] keys)
            {
                // Every key the app writes here is a string, except the consent version, which is an int
                // (read back as a string below would lose its type), so it is saved by type.
                _saved = keys.Select(key => (key, PlayerPrefs.HasKey(key), SavedValue(key))).ToArray();
            }

            public void Dispose()
            {
                if (_restored)
                {
                    return;
                }

                _restored = true;
                foreach (var (key, had, value) in _saved)
                {
                    if (!had)
                    {
                        PlayerPrefs.DeleteKey(key);
                    }
                    else if (key == CaptureConsent.PlayerPrefsVersionKey)
                    {
                        PlayerPrefs.SetInt(key, int.Parse(value));
                    }
                    else
                    {
                        PlayerPrefs.SetString(key, value);
                    }
                }

                PlayerPrefs.Save();
            }

            private static string SavedValue(string key)
            {
                return key == CaptureConsent.PlayerPrefsVersionKey
                    ? PlayerPrefs.GetInt(key, 0).ToString()
                    : PlayerPrefs.GetString(key, string.Empty);
            }
        }
    }
}
