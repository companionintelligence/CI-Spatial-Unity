using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using CompanionScan.Core;
using CompanionScan.Mapping;
using CompanionScan.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// No camera snapshot may leave the headset before the user agrees to the camera notice, for
    /// the server it names, and the agreement is recorded on the headset (Meta Developer Data Use
    /// Policy; V3 in the release audit).
    /// </summary>
    public class CaptureConsentTests
    {
        private const string ServerUrl = "https://cv.example.com";
        private const string OtherServerUrl = "https://other.example.com";
        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xD9 };

        private readonly Dictionary<string, (bool had, string value)> _savedStrings = new Dictionary<string, (bool, string)>();
        private bool _hadVersion;
        private int _savedVersion;
        private string _savedEnv;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private readonly List<(string url, Action next)> _requests = new List<(string, Action)>();
        private HashSet<GameObject> _sceneRoots;

        [SetUp]
        public void SetUp()
        {
            _hadVersion = PlayerPrefs.HasKey(CaptureConsent.PlayerPrefsVersionKey);
            _savedVersion = PlayerPrefs.GetInt(CaptureConsent.PlayerPrefsVersionKey, 0);
            foreach (var key in new[]
                     {
                         CaptureConsent.PlayerPrefsAgreedAtKey, CaptureConsent.PlayerPrefsServerUrlKey,
                         ServerConfig.PlayerPrefsBaseUrlKey
                     })
            {
                _savedStrings[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetString(key, string.Empty));
                PlayerPrefs.DeleteKey(key);
            }

            PlayerPrefs.DeleteKey(CaptureConsent.PlayerPrefsVersionKey);
            _savedEnv = Environment.GetEnvironmentVariable(ServerConfig.EditorServerUrlEnvVar);
            Environment.SetEnvironmentVariable(ServerConfig.EditorServerUrlEnvVar, null);
            _requests.Clear();
            _sceneRoots = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
        }

        [TearDown]
        public void TearDown()
        {
            CaptureConsent.Requested -= Record;

            // The settings build their world-space panel as a scene root of its own: remove it
            // before its owner.
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!_sceneRoots.Contains(root) && !_created.Contains(root))
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            foreach (var created in _created)
            {
                if (created != null)
                {
                    UnityEngine.Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            if (_hadVersion)
            {
                PlayerPrefs.SetInt(CaptureConsent.PlayerPrefsVersionKey, _savedVersion);
            }
            else
            {
                PlayerPrefs.DeleteKey(CaptureConsent.PlayerPrefsVersionKey);
            }

            foreach (var pair in _savedStrings)
            {
                if (pair.Value.had)
                {
                    PlayerPrefs.SetString(pair.Key, pair.Value.value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(pair.Key);
                }
            }

            PlayerPrefs.Save();
            Environment.SetEnvironmentVariable(ServerConfig.EditorServerUrlEnvVar, _savedEnv);
        }

        [Test]
        public void NothingIsAgreedUntilTheUserAgrees()
        {
            Assert.IsFalse(CaptureConsent.IsGranted);
            Assert.IsFalse(CaptureConsent.IsGrantedFor(ServerUrl));
            Assert.IsEmpty(CaptureConsent.AgreedAtUtc);
            Assert.IsEmpty(CaptureConsent.AgreedServerUrl);
        }

        [Test]
        public void Grant_RecordsTheNoticeVersionTheUtcTimeAndTheServerOnTheHeadset()
        {
            CaptureConsent.Grant(ServerUrl, new DateTime(2026, 11, 13, 23, 4, 5, DateTimeKind.Utc));

            Assert.IsTrue(CaptureConsent.IsGranted);
            Assert.AreEqual(CaptureConsent.NoticeVersion, PlayerPrefs.GetInt(CaptureConsent.PlayerPrefsVersionKey));
            Assert.AreEqual("2026-11-13T23:04:05Z", CaptureConsent.AgreedAtUtc);
            Assert.AreEqual(ServerUrl, CaptureConsent.AgreedServerUrl);
            Assert.AreEqual(ServerUrl, PlayerPrefs.GetString(CaptureConsent.PlayerPrefsServerUrlKey));
        }

        [Test]
        public void AnAgreementCoversOnlyTheServerItNamed()
        {
            CaptureConsent.Grant(ServerUrl);

            Assert.IsTrue(CaptureConsent.IsGrantedFor(ServerUrl));
            Assert.IsTrue(CaptureConsent.IsGrantedFor(" HTTPS://CV.example.com:443/ "), "the same server, written differently");
            Assert.IsFalse(CaptureConsent.IsGrantedFor(OtherServerUrl));
            Assert.IsFalse(CaptureConsent.IsGrantedFor("http://cv.example.com"));
            Assert.IsFalse(CaptureConsent.IsGrantedFor("https://cv.example.com/v2"));
            Assert.IsFalse(CaptureConsent.IsGrantedFor(string.Empty));
        }

        [Test]
        public void Grant_ForAnotherServer_ReplacesTheAgreement()
        {
            CaptureConsent.Grant(ServerUrl);
            CaptureConsent.Grant(OtherServerUrl);

            Assert.IsTrue(CaptureConsent.IsGrantedFor(OtherServerUrl));
            Assert.IsFalse(CaptureConsent.IsGrantedFor(ServerUrl));
        }

        [Test]
        public void Grant_WithoutAServer_RecordsNothing()
        {
            LogAssert.Expect(LogType.Warning, "[Consent] The notice named no vision server, so no agreement is recorded.");
            CaptureConsent.Grant(" ");

            Assert.IsFalse(CaptureConsent.IsGranted);
            Assert.IsFalse(PlayerPrefs.HasKey(CaptureConsent.PlayerPrefsVersionKey));
        }

        [Test]
        public void AnAgreementToAnOlderNotice_DoesNotCount()
        {
            CaptureConsent.Grant(ServerUrl);
            PlayerPrefs.SetInt(CaptureConsent.PlayerPrefsVersionKey, CaptureConsent.NoticeVersion - 1);

            Assert.IsFalse(CaptureConsent.IsGranted);
            Assert.IsFalse(CaptureConsent.IsGrantedFor(ServerUrl));
        }

        [Test]
        public void AnAgreementThatNamesNoServer_DoesNotCount()
        {
            // As recorded before the agreement named its server.
            PlayerPrefs.SetInt(CaptureConsent.PlayerPrefsVersionKey, CaptureConsent.NoticeVersion);
            PlayerPrefs.SetString(CaptureConsent.PlayerPrefsAgreedAtKey, "2026-11-13T23:04:05Z");

            Assert.IsFalse(CaptureConsent.IsGranted);
            Assert.IsFalse(CaptureConsent.IsGrantedFor(ServerUrl));
        }

        [Test]
        public void Withdraw_DeletesTheRecord()
        {
            CaptureConsent.Grant(ServerUrl);
            CaptureConsent.Withdraw();

            Assert.IsFalse(CaptureConsent.IsGranted);
            Assert.IsFalse(PlayerPrefs.HasKey(CaptureConsent.PlayerPrefsVersionKey));
            Assert.IsFalse(PlayerPrefs.HasKey(CaptureConsent.PlayerPrefsAgreedAtKey));
            Assert.IsFalse(PlayerPrefs.HasKey(CaptureConsent.PlayerPrefsServerUrlKey));
        }

        [Test]
        public void EnsureGranted_AsksWithTheServerAndTheContinuation_AndRefusesUntilAgreed()
        {
            CaptureConsent.Requested += Record;
            var continued = 0;

            Assert.IsFalse(CaptureConsent.EnsureGranted(ServerUrl, () => continued++));

            Assert.AreEqual(1, _requests.Count);
            Assert.AreEqual(ServerUrl, _requests[0].url);
            _requests[0].next();
            Assert.AreEqual(1, continued);
        }

        [Test]
        public void EnsureGranted_DoesNotAskOnceAgreed()
        {
            CaptureConsent.Grant(ServerUrl);
            CaptureConsent.Requested += Record;

            Assert.IsTrue(CaptureConsent.EnsureGranted(ServerUrl, () => { }));
            Assert.IsEmpty(_requests);
        }

        [Test]
        public void EnsureGranted_AsksAgainForANewServer()
        {
            CaptureConsent.Grant(ServerUrl);
            CaptureConsent.Requested += Record;

            Assert.IsFalse(CaptureConsent.EnsureGranted(OtherServerUrl, () => { }));

            Assert.AreEqual(1, _requests.Count);
            Assert.AreEqual(OtherServerUrl, _requests[0].url, "the notice must name the new server");
        }

        [Test]
        public void Pipeline_AsksAgainAfterTheServerChanged()
        {
            CaptureConsent.Grant(OtherServerUrl);
            CaptureConsent.Requested += Record;
            var camera = new CountingCameraProvider();
            var pipeline = NewPipeline(camera);

            RunCvPipeline(pipeline);

            Assert.AreEqual(0, camera.FramesTaken, "a frame was taken for a server the user did not agree to");
            Assert.AreEqual(1, _requests.Count, "the pipeline did not ask again");
            Assert.AreEqual(ServerUrl, _requests[0].url);
        }

        [Test]
        public void Pipeline_TakesNoFrameBeforeTheUserAgrees()
        {
            CaptureConsent.Requested += Record;
            var camera = new CountingCameraProvider();
            var pipeline = NewPipeline(camera);

            RunCvPipeline(pipeline);

            Assert.AreEqual(0, camera.FramesTaken, "a frame was taken before the user agreed");
            Assert.AreEqual(1, _requests.Count, "the pipeline did not ask for agreement");
            Assert.AreEqual(ServerUrl, _requests[0].url);
            Assert.IsNotNull(_requests[0].next, "agreeing must be able to continue the capture");
        }

        [Test]
        public void Pipeline_SendsNothingWhenNoNoticeCanAsk()
        {
            var camera = new CountingCameraProvider();
            var pipeline = NewPipeline(camera);

            LogAssert.Expect(LogType.Warning, "[Consent] No camera notice is available to ask for agreement, so no snapshot is sent.");
            RunCvPipeline(pipeline);

            Assert.AreEqual(0, camera.FramesTaken);
        }

        [UnityTest]
        public IEnumerator ImageRequest_WithoutAgreement_SendsNothing()
        {
            using var server = new LoopbackServer();
            var client = new CVServerClient(NewConfig(server.Url));

            var requests = new Task[]
            {
                client.DetectAsync(Jpeg, "chair ."),
                client.SegmentAsync(Jpeg, "chair ."),
                client.FlorenceAsync(Jpeg, "<MORE_DETAILED_CAPTION>")
            };

            foreach (var request in requests)
            {
                Assert.IsTrue(request.IsFaulted, "an image request went ahead without the agreement");
                Assert.IsInstanceOf<CaptureConsentRequiredException>(request.Exception.GetBaseException());
            }

            // Leave time for a request that got through to connect.
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!server.WasContacted && clock.Elapsed.TotalSeconds < 1)
            {
                yield return null;
            }

            Assert.IsFalse(server.WasContacted, "an image request reached the server without the agreement");
        }

        [UnityTest]
        public IEnumerator ImageRequest_ToAServerOtherThanTheOneAgreedTo_SendsNothing()
        {
            using var server = new LoopbackServer();
            var client = new CVServerClient(NewConfig(server.Url));
            CaptureConsent.Grant(OtherServerUrl);

            var request = client.DetectAsync(Jpeg, "chair .");

            Assert.IsTrue(request.IsFaulted, "an image request went ahead with the agreement for another server");
            Assert.IsInstanceOf<CaptureConsentRequiredException>(request.Exception.GetBaseException());
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!server.WasContacted && clock.Elapsed.TotalSeconds < 1)
            {
                yield return null;
            }

            Assert.IsFalse(server.WasContacted, "an image request reached a server the user did not agree to");
        }

        [UnityTest]
        public IEnumerator ImageRequest_WithTheAgreement_ReachesTheServer()
        {
            // The tests above mean something only if the same request, once agreed to, reaches the server.
            using var server = new LoopbackServer();
            var client = new CVServerClient(NewConfig(server.Url));
            CaptureConsent.Grant(server.Url);

            var request = client.DetectAsync(Jpeg, "chair .");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!server.WasContacted && clock.Elapsed.TotalSeconds < 10)
            {
                yield return null;
            }

            Assert.IsTrue(server.WasContacted, "the agreed request did not reach the test server");

            // Hang up without answering, so the client gives up before the test ends.
            server.Dispose();
            clock.Restart();
            while (!request.IsCompleted && clock.Elapsed.TotalSeconds < 10)
            {
                yield return null;
            }

            Assert.IsTrue(request.IsFaulted, "the client did not give up after the server hung up");
            Assert.IsNotInstanceOf<CaptureConsentRequiredException>(request.Exception.GetBaseException());
        }

        [Test]
        public void Notice_Accept_RecordsTheAgreementForTheServerItNamesAndContinuesTheCapture()
        {
            var notice = NewComponent<CaptureConsentPanel>();
            var continued = 0;

            notice.Show(ServerUrl, () => continued++);
            Assert.IsTrue(notice.IsOpen);
            notice.Accept();

            Assert.IsFalse(notice.IsOpen);
            Assert.IsTrue(CaptureConsent.IsGrantedFor(ServerUrl));
            Assert.IsFalse(CaptureConsent.IsGrantedFor(OtherServerUrl));
            Assert.AreEqual(1, continued);
        }

        [Test]
        public void Menu_OpeningTheSettings_ClosesTheNoticeWithoutAgreeing()
        {
            var settings = NewSettingsWithNotice(out var notice);
            var continued = 0;
            notice.Show(ServerUrl, () => continued++);

            settings.Toggle(); // what Menu does

            Assert.IsTrue(settings.IsOpen);
            Assert.IsFalse(notice.IsOpen, "the notice stayed open where the settings opened");
            Assert.IsFalse(CaptureConsent.IsGranted);
            Assert.AreEqual(0, continued);
        }

        [Test]
        public void Notice_DoesNotOpenOverTheSettings()
        {
            // A capture that started before the settings opened asks only once they are closed.
            var settings = NewSettingsWithNotice(out var notice);
            settings.Open();
            var continued = 0;

            notice.Show(ServerUrl, () => continued++);

            Assert.IsFalse(notice.IsOpen);
            Assert.IsTrue(settings.IsOpen);
            Assert.AreEqual(0, continued);
        }

        [Test]
        public void Notice_Decline_RecordsNothingAndSendsNothing()
        {
            var notice = NewComponent<CaptureConsentPanel>();
            var continued = 0;

            notice.Show(ServerUrl, () => continued++);
            notice.Decline();

            Assert.IsFalse(notice.IsOpen);
            Assert.IsFalse(CaptureConsent.IsGranted);
            Assert.AreEqual(0, continued);
        }

        [Test]
        public void Notice_SaysWhatIsSentWhereAndLinksThePolicy()
        {
            var notice = CaptureConsentPanel.BuildNotice(ServerUrl);

            StringAssert.Contains("one still image", notice);
            StringAssert.Contains("HTTPS", notice);
            StringAssert.Contains(ServerUrl, notice);
            StringAssert.Contains("Companion Intelligence receives none of it", notice);
            StringAssert.Contains("exports stay on this headset", notice);
            StringAssert.Contains("withdraw", notice);
            StringAssert.Contains("covers this server only", notice);
            StringAssert.Contains(PrivacyPolicy.DisplayUrl, notice);
            StringAssert.DoesNotContain("upload", notice.ToLowerInvariant());
        }

        [Test]
        public void PrivacyPolicy_IsThePublishedHttpsPage()
        {
            Assert.AreEqual("https://ci.computer/privacy/companion-scan", PrivacyPolicy.Url);
            Assert.AreEqual("ci.computer/privacy/companion-scan", PrivacyPolicy.DisplayUrl);
        }

        [Test]
        public void Settings_ShowWhetherAndWhenTheUserAgreedForTheServer()
        {
            StringAssert.Contains("asks you first", SettingsPanel.DescribeCaptureConsent(ServerUrl));

            CaptureConsent.Grant(ServerUrl, new DateTime(2026, 11, 13, 8, 0, 0, DateTimeKind.Utc));

            StringAssert.Contains("you agreed on 2026-11-13", SettingsPanel.DescribeCaptureConsent(ServerUrl));
            StringAssert.Contains("the next capture asks for this server", SettingsPanel.DescribeCaptureConsent(OtherServerUrl));
        }

        private void Record(string url, Action next)
        {
            _requests.Add((url, next));
        }

        private T NewComponent<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            _created.Add(go);
            return go.AddComponent<T>();
        }

        /// <summary>The settings with the camera notice next to them, as SettingsPanel sets them up at run time.</summary>
        private SettingsPanel NewSettingsWithNotice(out CaptureConsentPanel notice)
        {
            var settings = NewComponent<SettingsPanel>();
            settings.SetConfig(NewConfig(ServerUrl));
            notice = settings.gameObject.AddComponent<CaptureConsentPanel>();
            return settings;
        }

        private ServerConfig NewConfig(string serverUrl)
        {
            var config = ScriptableObject.CreateInstance<ServerConfig>();
            config.baseUrl = serverUrl;
            config.requestTimeoutSeconds = 2;
            config.retryCount = 0;
            _created.Add(config);
            return config;
        }

        private SpatialCapturePipeline NewPipeline(ICameraProvider camera)
        {
            var pipeline = NewComponent<SpatialCapturePipeline>();
            SetField(pipeline, "serverConfig", NewConfig(ServerUrl));
            SetField(pipeline, "_cameraProvider", camera);
            return pipeline;
        }

        private static void RunCvPipeline(SpatialCapturePipeline pipeline)
        {
            var method = typeof(SpatialCapturePipeline).GetMethod("RunCvPipelineAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "SpatialCapturePipeline.RunCvPipelineAsync not found");
            var task = (Task)method.Invoke(pipeline, null);
            Assert.IsTrue(task.IsCompleted, "the pipeline went past the agreement check");
            task.GetAwaiter().GetResult();
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} not found");
            field.SetValue(target, value);
        }

        /// <summary>
        /// A port on this machine that notes any connection. Its URL is https, which every build
        /// allows; a client connects before the TLS handshake, which never gets an answer.
        /// </summary>
        private sealed class LoopbackServer : IDisposable
        {
            private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
            private bool _contacted;
            private bool _stopped;

            public LoopbackServer()
            {
                _listener.Start();
                Url = "https://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port;
            }

            public string Url { get; }

            /// <summary>True once a client has connected. Connections are never accepted or answered.</summary>
            public bool WasContacted
            {
                get
                {
                    if (!_contacted && !_stopped && _listener.Pending())
                    {
                        _contacted = true;
                    }

                    return _contacted;
                }
            }

            /// <summary>Stops listening, which resets any connection still waiting.</summary>
            public void Dispose()
            {
                if (!_stopped)
                {
                    _stopped = true;
                    _listener.Stop();
                }
            }
        }

        private sealed class CountingCameraProvider : ICameraProvider
        {
            public int FramesTaken { get; private set; }
            public bool IsAvailable => true;

            public void StartCapture()
            {
            }

            public void StopCapture()
            {
            }

            public byte[] GetCurrentFrameJpeg(int quality = 75)
            {
                FramesTaken++;
                return new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
            }

            public Matrix4x4 GetCameraIntrinsics() => Matrix4x4.identity;

            public Pose GetCameraPose() => Pose.identity;
        }
    }
}
