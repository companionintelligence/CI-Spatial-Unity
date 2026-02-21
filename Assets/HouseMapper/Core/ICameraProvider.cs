using UnityEngine;

namespace HouseMapper.Core
{
    public interface ICameraProvider
    {
        bool IsAvailable { get; }
        void StartCapture();
        void StopCapture();
        byte[] GetCurrentFrameJpeg(int quality = 75);
        Matrix4x4 GetCameraIntrinsics();
        Pose GetCameraPose();
    }
}
