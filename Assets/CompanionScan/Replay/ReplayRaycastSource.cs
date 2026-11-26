using System.Collections.Generic;
using CompanionScan.Mapping;
using UnityEngine;

namespace CompanionScan.Replay
{
    /// <summary>
    /// The raycast backend of a recorded session: a ray meets the recorded room's planes. The nearest
    /// plane the ray reaches from the front, within the maximum distance, is the hit; it counts as an
    /// environment raycast, as the headset's depth raycast would.
    /// </summary>
    public sealed class ReplayRaycastSource : IRaycastSource
    {
        private readonly IReadOnlyList<ReplayPlane> _planes;

        public ReplayRaycastSource(IReadOnlyList<ReplayPlane> planes)
        {
            _planes = planes ?? new List<ReplayPlane>();
        }

        public bool TryRaycast(Ray ray, float maxDistance, out RaycastSample hit)
        {
            float bestDistance = float.MaxValue;
            ReplayPlane best = null;

            foreach (var plane in _planes)
            {
                var normal = plane.normal.normalized;
                float facing = Vector3.Dot(normal, ray.direction);

                // Only a plane the ray meets from its front: a ray leaving the room does not "hit" the wall behind it.
                if (facing >= -1e-6f)
                {
                    continue;
                }

                float distance = Vector3.Dot(plane.point - ray.origin, normal) / facing;
                if (distance <= 0f || distance > maxDistance || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = plane;
            }

            if (best == null)
            {
                hit = default;
                return false;
            }

            hit = new RaycastSample(ray.origin + ray.direction * bestDistance, best.normal.normalized, RaycastHitType.Environment);
            return true;
        }
    }
}
