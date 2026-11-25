using UnityEngine;

namespace CompanionScan.Mapping
{
    /// <summary>
    /// Chooses the pose a capture frame is stamped with. The camera provider knows which camera took
    /// the frame, and its pose is preferred. A provider that reports the world origin with no rotation
    /// while the headset is somewhere else is a transform that does not follow the headset (the
    /// camera object the pipeline creates used to sit under a root that never moves), and the
    /// headset's own pose is used instead.
    /// </summary>
    public static class CapturePose
    {
        /// <summary>Within this distance (metres) of the origin a pose counts as the world origin.</summary>
        public const float OriginTolerance = 1e-4f;

        /// <summary>The headset must be further than this (metres) from the origin, or turned further than <see cref="MovedMinAngle"/>, before a provider at the origin is rejected.</summary>
        public const float MovedMinDistance = 0.05f;

        /// <summary>See <see cref="MovedMinDistance"/>. Degrees.</summary>
        public const float MovedMinAngle = 1f;

        /// <summary>True when the pose is the world origin with no rotation.</summary>
        public static bool IsWorldOrigin(Pose pose)
        {
            return pose.position.sqrMagnitude <= OriginTolerance * OriginTolerance
                   && Quaternion.Angle(pose.rotation, Quaternion.identity) <= 0.01f;
        }

        /// <summary>
        /// The pose to stamp on a frame. <paramref name="providerRejected"/> is true when the provider
        /// reported the world origin while the camera is elsewhere, and the camera's pose is returned.
        /// </summary>
        public static Pose Resolve(Pose providerPose, bool hasCamera, Pose cameraPose, out bool providerRejected)
        {
            providerRejected = false;
            if (!hasCamera || !IsWorldOrigin(providerPose))
            {
                return providerPose;
            }

            bool cameraMoved = cameraPose.position.magnitude > MovedMinDistance
                               || Quaternion.Angle(cameraPose.rotation, Quaternion.identity) > MovedMinAngle;
            if (!cameraMoved)
            {
                return providerPose;
            }

            providerRejected = true;
            return cameraPose;
        }
    }
}
