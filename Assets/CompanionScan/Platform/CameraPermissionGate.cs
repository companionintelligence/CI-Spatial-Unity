using System;

namespace CompanionScan.Platform
{
    /// <summary>
    /// Runs a camera only while its run-time permissions are granted.
    ///
    /// Meta OpenXR's camera subsystem, which serves AR Foundation's ARCameraManager on Meta Quest,
    /// checks horizonos.permission.HEADSET_CAMERA once, in Start(), when the ARCameraManager is
    /// enabled. Started without the permission it delivers no frames until it is stopped and started
    /// again, and nothing restarts it after the user allows it. The gate therefore keeps the camera
    /// off until the permissions are granted (stopping one that already runs without them), asks for
    /// them when capture starts, and switches the camera on once they are granted, by its own request,
    /// a later one or the system settings. Switching it on starts the subsystem, which checks again.
    ///
    /// Plain C# with injected callbacks so the logic can be tested in the editor. The camera
    /// providers drive it in Android players.
    /// </summary>
    public sealed class CameraPermissionGate
    {
        /// <summary>How often a waiting gate asks the OS whether the permissions are granted now.</summary>
        public const float PollIntervalSeconds = 0.5f;

        private readonly Func<bool> _isGranted;
        private readonly Action _request;
        private readonly Func<bool> _isCameraOn;
        private readonly Action<bool> _setCameraOn;
        private float _nextPoll = float.NegativeInfinity;

        /// <param name="isGranted">Whether every permission the camera needs is granted (may query the OS).</param>
        /// <param name="request">Asks for the missing permissions; a no-op while a request is open or after a denial.</param>
        /// <param name="isCameraOn">Whether the camera is switched on.</param>
        /// <param name="setCameraOn">Switches the camera on or off.</param>
        public CameraPermissionGate(Func<bool> isGranted, Action request, Func<bool> isCameraOn, Action<bool> setCameraOn)
        {
            _isGranted = isGranted ?? throw new ArgumentNullException(nameof(isGranted));
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _isCameraOn = isCameraOn ?? throw new ArgumentNullException(nameof(isCameraOn));
            _setCameraOn = setCameraOn ?? throw new ArgumentNullException(nameof(setCameraOn));
        }

        /// <summary>True between <see cref="StartCapture"/> and <see cref="StopCapture"/>.</summary>
        public bool IsCaptureRequested { get; private set; }

        /// <summary>True while capture is requested but the camera waits for its permissions.</summary>
        public bool IsWaitingForPermission => IsCaptureRequested && !_isCameraOn();

        /// <summary>
        /// Call once, before capture starts. A camera that already runs without the permissions (for
        /// example one enabled when its component was added) is switched off, so that it starts, and
        /// checks again, once they are granted.
        /// </summary>
        public void Initialize()
        {
            if (_isCameraOn() && !_isGranted())
            {
                _setCameraOn(false);
            }
        }

        /// <summary>Switches the camera on if the permissions are granted; otherwise asks for them.</summary>
        public void StartCapture(float now)
        {
            IsCaptureRequested = true;
            if (_isGranted())
            {
                if (!_isCameraOn())
                {
                    _setCameraOn(true);
                }

                return;
            }

            if (_isCameraOn())
            {
                _setCameraOn(false);
            }

            _request();
            _nextPoll = now + PollIntervalSeconds;
        }

        public void StopCapture()
        {
            IsCaptureRequested = false;
            if (_isCameraOn())
            {
                _setCameraOn(false);
            }
        }

        /// <summary>Call every frame: switches the camera on once the permissions are granted.</summary>
        public void Update(float now)
        {
            if (!IsCaptureRequested || _isCameraOn() || now < _nextPoll)
            {
                return;
            }

            _nextPoll = now + PollIntervalSeconds;
            if (_isGranted())
            {
                _setCameraOn(true);
            }
        }

        /// <summary>
        /// Capture wants a frame while the camera still waits: ask again, for example after a dismissed
        /// dialog. The request is a no-op while one is open and after a denial.
        /// </summary>
        public void OnFrameRequested()
        {
            if (IsWaitingForPermission)
            {
                _request();
            }
        }
    }
}
