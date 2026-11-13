using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace CompanionScan.Core
{
    /// <summary>
    /// Run-time permissions for the Meta Quest 3 / 3S passthrough cameras, shared by both capture
    /// paths: <c>MobileCameraProvider</c> (AR Foundation and Meta OpenXR's camera subsystem, the path
    /// the shipped scene uses) and the <c>QuestCameraProvider</c> WebCamTexture fallback.
    /// <list type="bullet">
    ///   <item><see cref="HeadsetCamera"/> gates the passthrough cameras (Passthrough Camera API,
    ///     Horizon OS v74+). Meta OpenXR's camera subsystem checks it once, when it starts.</item>
    ///   <item><see cref="AndroidCamera"/>: Meta OpenXR opens the passthrough cameras through
    ///     Android's NDK Camera2 API (ACameraManager_openCamera), which checks it, and Unity's
    ///     WebCamTexture checks it too. Meta OpenXR also adds it to the merged manifest itself while
    ///     camera image support is on. Keep it until capture is verified on a headset without it.</item>
    /// </list>
    /// Both are requested in one call when capture starts. This class is the only code that asks
    /// for them (OVRManager's startup request is off in the scene), so a user is not asked twice.
    /// A denial is remembered for the rest of the session so capture does not prompt again; a grant
    /// made later in the system settings is picked up automatically. Outside Android players the
    /// permissions do not apply and are reported as granted.
    /// </summary>
    public static class PassthroughCameraPermission
    {
        public const string HeadsetCamera = "horizonos.permission.HEADSET_CAMERA";
        public const string AndroidCamera = "android.permission.CAMERA";

        /// <summary>Run-time permissions the passthrough cameras need, in the order they are requested.</summary>
        public static readonly IReadOnlyList<string> RequiredPermissions = new[] { HeadsetCamera, AndroidCamera };

        public const string DeniedMessage =
            "Camera permission denied: frame capture is off. Allow the headset cameras for this app in " +
            "the headset settings (Privacy) to capture frames.";

        public enum State
        {
            Unknown,
            Requesting,
            Granted,
            Denied,
            NotRequired
        }

        private static State s_state = State.Unknown;
#if UNITY_ANDROID && !UNITY_EDITOR
        private static TaskCompletionSource<bool> s_pending;
#endif

        /// <summary>Last observed state, without querying the OS (cheap; safe to poll every frame).</summary>
        public static State LastKnown => s_state;

        /// <summary>True when every permission in <see cref="RequiredPermissions"/> is granted (queries the OS when not yet granted).</summary>
        public static bool IsGranted
        {
            get
            {
                Refresh();
                return s_state == State.Granted || s_state == State.NotRequired;
            }
        }

        /// <summary>
        /// Asks for the permissions that are still missing, all in one request. Resolves to
        /// <c>true</c> when all are granted. Concurrent callers share one request; after a denial it
        /// resolves to <c>false</c> without asking again.
        /// </summary>
        public static Task<bool> RequestAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Refresh();
            if (s_state == State.Granted)
            {
                return Task.FromResult(true);
            }

            if (s_state == State.Denied)
            {
                return Task.FromResult(false);
            }

            if (s_pending != null)
            {
                return s_pending.Task;
            }

            var missing = new List<string>();
            foreach (var permission in RequiredPermissions)
            {
                if (!Permission.HasUserAuthorizedPermission(permission))
                {
                    missing.Add(permission);
                }
            }

            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            s_pending = pending;
            s_state = State.Requesting;

            var unanswered = new HashSet<string>(missing);
            var denied = false;
            var dismissed = false;

            void Answer(string permission, bool granted, bool wasDismissed)
            {
                if (!unanswered.Remove(permission))
                {
                    return;
                }

                denied |= !granted && !wasDismissed;
                dismissed |= wasDismissed;
                if (unanswered.Count == 0)
                {
                    Complete(pending, denied ? State.Denied : dismissed ? State.Unknown : State.Granted);
                }
            }

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += permission => Answer(permission, true, false);
            // Also raised for "don't ask again" while PermissionDeniedAndDontAskAgain
            // (obsolete in Unity 6) has no subscribers.
            callbacks.PermissionDenied += permission => Answer(permission, false, false);
            // Dialog dismissed without an answer (e.g. focus lost): ask again next time capture needs it.
            callbacks.PermissionRequestDismissed += permission => Answer(permission, false, true);

            try
            {
                Debug.Log("[PassthroughCamera] Requesting " + string.Join(", ", missing));
                Permission.RequestUserPermissions(missing.ToArray(), callbacks);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PassthroughCamera] Permission request failed: " + e.Message);
                Complete(pending, State.Denied);
            }

            return pending.Task;
#else
            s_state = State.NotRequired;
            return Task.FromResult(true);
#endif
        }

        /// <summary>Starts <see cref="RequestAsync"/> without waiting (for synchronous call sites).</summary>
        public static void RequestInBackground()
        {
            _ = RequestAsync();
        }

        private static void Refresh()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Also checked while a request is open, so a grant is seen even before its callbacks arrive.
            if (s_state == State.Granted)
            {
                return;
            }

            foreach (var permission in RequiredPermissions)
            {
                if (!Permission.HasUserAuthorizedPermission(permission))
                {
                    return;
                }
            }

            s_state = State.Granted;
#else
            s_state = State.NotRequired;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void Complete(TaskCompletionSource<bool> pending, State state)
        {
            if (s_pending == pending)
            {
                s_pending = null;
            }

            s_state = state;
            if (state == State.Granted)
            {
                Debug.Log("[PassthroughCamera] Permissions granted.");
            }
            else if (state == State.Denied)
            {
                Debug.LogWarning("[PassthroughCamera] " + DeniedMessage);
            }

            pending.TrySetResult(state == State.Granted);
        }
#endif
    }
}
