using System;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace CompanionScan.Core
{
    /// <summary>
    /// Horizon OS "spatial data" permission (<c>com.oculus.permission.USE_SCENE</c>),
    /// which gates the scene model (room layout, planes, meshes) and environment
    /// raycasts.
    ///
    /// The permission is requested the first time a feature needs it. A denial is
    /// remembered for the rest of the session so captures do not prompt again; a
    /// grant made later in the system settings is picked up automatically. Outside
    /// Android players the permission does not apply and is reported as granted.
    /// </summary>
    public static class SpatialDataPermission
    {
        public const string PermissionId = "com.oculus.permission.USE_SCENE";

        public const string DeniedMessage =
            "Spatial data permission denied: room layout and environment raycasts are off. " +
            "Allow \"Spatial data\" for this app in the headset settings (Privacy) to enable them.";

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

        /// <summary>True when spatial data may be used now (queries the OS when not yet granted).</summary>
        public static bool IsGranted
        {
            get
            {
                Refresh();
                return s_state == State.Granted || s_state == State.NotRequired;
            }
        }

        /// <summary>
        /// Ensures the permission has been requested. Shows the system dialog the
        /// first time; resolves to <c>true</c> when granted. Concurrent callers share
        /// one request.
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

            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            s_pending = pending;
            s_state = State.Requesting;

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => Complete(pending, State.Granted);
            // Also raised for "don't ask again" while PermissionDeniedAndDontAskAgain
            // (obsolete in Unity 6) has no subscribers.
            callbacks.PermissionDenied += _ => Complete(pending, State.Denied);
            // Dialog dismissed without an answer (e.g. focus lost): ask again next time it is needed.
            callbacks.PermissionRequestDismissed += _ => Complete(pending, State.Unknown);

            try
            {
                Debug.Log("[SpatialData] Requesting " + PermissionId);
                Permission.RequestUserPermission(PermissionId, callbacks);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SpatialData] Permission request failed: " + e.Message);
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
            if (s_state != State.Granted && s_state != State.Requesting &&
                Permission.HasUserAuthorizedPermission(PermissionId))
            {
                s_state = State.Granted;
            }
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
                Debug.Log("[SpatialData] Permission granted.");
            }
            else if (state == State.Denied)
            {
                Debug.LogWarning("[SpatialData] " + DeniedMessage);
            }

            pending.TrySetResult(state == State.Granted);
        }
#endif
    }
}
