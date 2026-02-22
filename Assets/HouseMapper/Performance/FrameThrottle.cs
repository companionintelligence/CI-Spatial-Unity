using UnityEngine;

namespace HouseMapper.Performance
{
    public class FrameThrottle : MonoBehaviour
    {
        [SerializeField] private float minIntervalSeconds = 0.2f;
        [SerializeField] private float movementThreshold = 0.02f;
        [SerializeField] private float rotationThreshold = 2.0f;

        private float _lastSentTime;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private bool _hasSample;

        public bool ShouldSendFrame(Transform cameraTransform)
        {
            if (cameraTransform == null)
            {
                return false;
            }

            if (Time.time - _lastSentTime < minIntervalSeconds)
            {
                return false;
            }

            if (!_hasSample)
            {
                return true;
            }

            float moveDelta = Vector3.Distance(_lastPosition, cameraTransform.position);
            float rotDelta = Quaternion.Angle(_lastRotation, cameraTransform.rotation);
            return moveDelta >= movementThreshold || rotDelta >= rotationThreshold;
        }

        public void MarkSent(Transform cameraTransform)
        {
            if (cameraTransform == null)
            {
                return;
            }

            _lastSentTime = Time.time;
            _lastPosition = cameraTransform.position;
            _lastRotation = cameraTransform.rotation;
            _hasSample = true;
        }
    }
}
