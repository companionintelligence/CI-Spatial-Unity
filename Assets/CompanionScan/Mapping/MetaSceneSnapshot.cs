using System;
using System.Collections.Generic;
using UnityEngine;

namespace CompanionScan.Mapping
{
    [Serializable]
    public class MetaSceneAnchorSnapshot
    {
        public string uuid;
        public List<string> labels = new List<string>();
        public Vector3 position;
        public Quaternion rotation;
        public bool hasPlane;
        public Vector3 planeCenter;
        public Vector3 planeSize;
        public List<Vector3> planeBoundary = new List<Vector3>();
        public bool hasVolume;
        public Vector3 volumeCenter;
        public Vector3 volumeSize;
        public bool hasMesh;
        public int meshVertexCount;
        public int meshTriangleCount;
    }

    [Serializable]
    public class MetaSceneSnapshot
    {
        public List<MetaSceneAnchorSnapshot> anchors = new List<MetaSceneAnchorSnapshot>();
        public List<string> supportedLabels = new List<string>();
        public List<string> roomUuids = new List<string>();
        public string primaryRoomUuid;
        public double timestamp;
        public string error;
    }
}

