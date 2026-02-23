using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace CompanionScan.Mapping
{
    public class BoundaryProvider : MonoBehaviour
    {
        [SerializeField] private Transform trackingOrigin;

        public List<Vector3> GetBoundaryWorldPoints()
        {
            var points = new List<Vector3>();
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);

            for (int i = 0; i < subsystems.Count; i++)
            {
                if (subsystems[i].TryGetBoundaryPoints(points) && points.Count > 0)
                {
                    return TransformToWorld(points);
                }
            }

            return points;
        }

        private List<Vector3> TransformToWorld(List<Vector3> localPoints)
        {
            if (trackingOrigin == null)
            {
                return localPoints;
            }

            var worldPoints = new List<Vector3>(localPoints.Count);
            for (int i = 0; i < localPoints.Count; i++)
            {
                worldPoints.Add(trackingOrigin.TransformPoint(localPoints[i]));
            }

            return worldPoints;
        }
    }
}

