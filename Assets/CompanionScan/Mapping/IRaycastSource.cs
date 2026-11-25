using UnityEngine;

namespace CompanionScan.Mapping
{
    /// <summary>One hit of an <see cref="IRaycastSource"/>.</summary>
    public readonly struct RaycastSample
    {
        public RaycastSample(Vector3 point, Vector3 normal, RaycastHitType type)
        {
            Point = point;
            Normal = normal;
            Type = type;
        }

        public Vector3 Point { get; }

        public Vector3 Normal { get; }

        /// <summary>What was hit; it also decides how much the pin's placement is trusted.</summary>
        public RaycastHitType Type { get; }
    }

    /// <summary>
    /// A way to find where a ray meets the room: an environment depth raycast, AR planes, a scene mesh,
    /// physics colliders or a recorded room. Sources registered with
    /// <see cref="ViewportRaycaster.AddSource"/> are asked first, in the order they were added.
    /// </summary>
    public interface IRaycastSource
    {
        /// <summary>The nearest hit along <paramref name="ray"/> within <paramref name="maxDistance"/> metres.</summary>
        bool TryRaycast(Ray ray, float maxDistance, out RaycastSample hit);
    }
}
