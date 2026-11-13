using CompanionScan.Platform;
using NUnit.Framework;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// The gate in front of Meta OpenXR's camera subsystem, which checks the headset camera permission
    /// only when it starts (when the ARCameraManager is enabled). "Starts" counts off-to-on switches:
    /// each one starts the subsystem, which checks the permission again.
    /// </summary>
    public class CameraPermissionGateTests
    {
        private sealed class FakeCamera
        {
            public bool Granted;
            public bool On;
            public int Requests;
            public int GrantChecks;
            public int Starts;
            public int Stops;

            public CameraPermissionGate CreateGate()
            {
                return new CameraPermissionGate(
                    () =>
                    {
                        GrantChecks++;
                        return Granted;
                    },
                    () => Requests++,
                    () => On,
                    on =>
                    {
                        if (on && !On)
                        {
                            Starts++;
                        }

                        if (!on && On)
                        {
                            Stops++;
                        }

                        On = on;
                    });
            }
        }

        [Test]
        public void FirstLaunch_StartsTheCameraOnlyAfterThePermissionsAreGranted()
        {
            // Switched on when its component was added, before the gate and without the permission:
            // the state the reviewed build reached on first launch.
            var camera = new FakeCamera { Granted = false, On = true };
            var gate = camera.CreateGate();

            gate.Initialize();
            Assert.IsFalse(camera.On, "a camera running without the permissions must stop so that it can start again");

            gate.StartCapture(0f);
            Assert.AreEqual(1, camera.Requests);
            Assert.IsFalse(camera.On);
            Assert.IsTrue(gate.IsWaitingForPermission);

            gate.Update(1f);
            Assert.IsFalse(camera.On, "not granted yet");

            camera.Granted = true; // the user allows the dialog
            gate.Update(2f);
            Assert.IsTrue(camera.On);
            Assert.AreEqual(1, camera.Starts, "the camera (and so the subsystem's permission check) starts after the grant");
            Assert.IsFalse(gate.IsWaitingForPermission);
            Assert.AreEqual(1, camera.Requests, "asked once");
        }

        [Test]
        public void LaterLaunch_StartsTheCameraWithoutAsking()
        {
            var camera = new FakeCamera { Granted = true };
            var gate = camera.CreateGate();

            gate.Initialize();
            gate.StartCapture(0f);

            Assert.IsTrue(camera.On);
            Assert.AreEqual(0, camera.Requests);
        }

        [Test]
        public void Initialize_KeepsACameraThatRunsWithThePermissions()
        {
            var camera = new FakeCamera { Granted = true, On = true };

            camera.CreateGate().Initialize();

            Assert.IsTrue(camera.On);
            Assert.AreEqual(0, camera.Stops);
        }

        [Test]
        public void Update_DoesNothingBeforeCaptureStarts()
        {
            var camera = new FakeCamera { Granted = true };
            var gate = camera.CreateGate();

            gate.Initialize();
            gate.Update(10f);

            Assert.IsFalse(camera.On);
            Assert.IsFalse(gate.IsWaitingForPermission);
        }

        [Test]
        public void Update_AsksTheOsAtMostOncePerPollInterval()
        {
            var camera = new FakeCamera();
            var gate = camera.CreateGate();
            gate.StartCapture(0f);
            var checks = camera.GrantChecks;

            gate.Update(CameraPermissionGate.PollIntervalSeconds * 0.5f);
            Assert.AreEqual(checks, camera.GrantChecks, "too early");

            gate.Update(CameraPermissionGate.PollIntervalSeconds);
            Assert.AreEqual(checks + 1, camera.GrantChecks);

            gate.Update(CameraPermissionGate.PollIntervalSeconds * 1.5f);
            Assert.AreEqual(checks + 1, camera.GrantChecks, "too early again");
        }

        [Test]
        public void ADenial_KeepsTheCameraOff_AndALaterGrantInSettingsStartsIt()
        {
            var camera = new FakeCamera();
            var gate = camera.CreateGate();
            gate.StartCapture(0f);

            for (var t = 1f; t < 30f; t += 1f)
            {
                gate.Update(t);
            }

            Assert.IsFalse(camera.On);
            Assert.AreEqual(1, camera.Requests, "the gate does not ask again by itself");

            camera.Granted = true; // allowed later in the headset settings
            gate.Update(31f);
            Assert.IsTrue(camera.On);
        }

        [Test]
        public void StopCapture_SwitchesTheCameraOff_AndUpdateKeepsItOff()
        {
            var camera = new FakeCamera { Granted = true };
            var gate = camera.CreateGate();
            gate.StartCapture(0f);

            gate.StopCapture();
            gate.Update(10f);

            Assert.IsFalse(camera.On);
            Assert.IsFalse(gate.IsCaptureRequested);
        }

        [Test]
        public void StopCapture_WhileWaiting_IgnoresALaterGrant()
        {
            var camera = new FakeCamera();
            var gate = camera.CreateGate();
            gate.StartCapture(0f);

            gate.StopCapture();
            camera.Granted = true;
            gate.Update(10f);

            Assert.IsFalse(camera.On);
        }

        [Test]
        public void OnFrameRequested_AsksAgainOnlyWhileWaiting()
        {
            var camera = new FakeCamera();
            var gate = camera.CreateGate();

            gate.OnFrameRequested();
            Assert.AreEqual(0, camera.Requests, "capture has not started");

            gate.StartCapture(0f);
            gate.OnFrameRequested(); // e.g. after a dismissed dialog
            Assert.AreEqual(2, camera.Requests);

            camera.Granted = true;
            gate.Update(1f);
            gate.OnFrameRequested();
            Assert.AreEqual(2, camera.Requests, "the camera runs: nothing to ask for");
        }

        [Test]
        public void StartCapture_StopsACameraThatRunsWithoutThePermissions()
        {
            // A camera switched on behind the gate's back, before the permissions were granted.
            var camera = new FakeCamera();
            var gate = camera.CreateGate();
            camera.On = true;

            gate.StartCapture(0f);

            Assert.IsFalse(camera.On);
            camera.Granted = true;
            gate.Update(1f);
            Assert.IsTrue(camera.On);
            Assert.AreEqual(1, camera.Stops);
        }
    }
}
