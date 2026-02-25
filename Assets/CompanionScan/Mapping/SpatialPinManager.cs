using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace CompanionScan.Mapping
{
    /// <summary>
    /// Central store for <see cref="SpatialPin"/>s from both capture systems.
    /// Spawns a world-space label prefab for each pin.
    /// </summary>
    public class SpatialPinManager : MonoBehaviour
    {
        [SerializeField] private Transform pinRoot;
        [SerializeField] private GameObject pinPrefab;
        [SerializeField] private Vector3 pinScale = Vector3.one;
        [SerializeField] private bool enableDeduplication = true;
        [SerializeField] private float dedupDistance = 0.15f;
        [SerializeField] private bool enablePinLifecycle = true;
        [SerializeField] private float pinMaxAgeSeconds = 6f;
        [SerializeField] private bool preferTrackIds = true;

        private readonly List<SpatialPin> _pins = new List<SpatialPin>();
        private readonly List<GameObject> _instances = new List<GameObject>();

        /// <summary>All pins from every source.</summary>
        public IReadOnlyList<SpatialPin> Pins => _pins;

        /// <summary>The root transform under which pin instances are spawned.</summary>
        public Transform PinRoot => pinRoot != null ? pinRoot : transform;

        /// <summary>Only pins produced by the CV server pipeline.</summary>
        public IReadOnlyList<SpatialPin> CvPins =>
            _pins.Where(p => p.source == PinSource.CvServer).ToList();

        /// <summary>Only pins produced by Meta Scene Understanding.</summary>
        public IReadOnlyList<SpatialPin> MetaScenePins =>
            _pins.Where(p => p.source == PinSource.MetaScene).ToList();

        /// <summary>Remove all pins and destroy their GameObjects.</summary>
        public void ClearPins()
        {
            for (int i = 0; i < _instances.Count; i++)
            {
                if (_instances[i] != null) Destroy(_instances[i]);
            }
            _instances.Clear();
            _pins.Clear();
        }

        /// <summary>Remove only pins from a specific source.</summary>
        public void ClearPins(PinSource source)
        {
            for (int i = _pins.Count - 1; i >= 0; i--)
            {
                if (_pins[i].source == source)
                {
                    if (i < _instances.Count && _instances[i] != null)
                    {
                        Destroy(_instances[i]);
                        _instances.RemoveAt(i);
                    }
                    _pins.RemoveAt(i);
                }
            }
        }

        public void AddPin(SpatialPin pin)
        {
            if (enablePinLifecycle)
            {
                PruneStalePins(pin.timestamp);
            }

            if (preferTrackIds && pin.trackId != 0 && TryMergeTrack(pin))
            {
                return;
            }

            if (enableDeduplication && TryMergePin(pin))
            {
                return;
            }

            _pins.Add(pin);
            SpawnPin(pin);
        }

        private bool TryMergePin(SpatialPin pin)
        {
            if (string.IsNullOrWhiteSpace(pin.semanticLabel))
            {
                return false;
            }

            for (int i = 0; i < _pins.Count; i++)
            {
                var existing = _pins[i];
                if (existing.source != pin.source)
                {
                    continue;
                }
                if (!string.Equals(existing.semanticLabel, pin.semanticLabel, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Vector3.Distance(existing.position, pin.position) > dedupDistance)
                {
                    continue;
                }

                existing.position = Vector3.Lerp(existing.position, pin.position, 0.5f);
                existing.normal = Vector3.Lerp(existing.normal, pin.normal, 0.5f).normalized;
                existing.score = Mathf.Max(existing.score, pin.score);
                existing.timestamp = Math.Max(existing.timestamp, pin.timestamp);
                existing.placementConfidence = Mathf.Max(existing.placementConfidence, pin.placementConfidence);
                existing.captureId = pin.captureId ?? existing.captureId;
                existing.captureSessionId = pin.captureSessionId ?? existing.captureSessionId;
                existing.captureFrameIndex = pin.captureFrameIndex != 0 ? pin.captureFrameIndex : existing.captureFrameIndex;
                existing.trackId = pin.trackId != 0 ? pin.trackId : existing.trackId;
                existing.hitType = pin.hitType;

                _pins[i] = existing;
                UpdateInstance(i, existing);
                return true;
            }

            return false;
        }

        private bool TryMergeTrack(SpatialPin pin)
        {
            for (int i = 0; i < _pins.Count; i++)
            {
                var existing = _pins[i];
                if (existing.trackId != pin.trackId)
                {
                    continue;
                }

                existing.position = Vector3.Lerp(existing.position, pin.position, 0.5f);
                existing.normal = Vector3.Lerp(existing.normal, pin.normal, 0.5f).normalized;
                existing.score = Mathf.Max(existing.score, pin.score);
                existing.timestamp = Math.Max(existing.timestamp, pin.timestamp);
                existing.placementConfidence = Mathf.Max(existing.placementConfidence, pin.placementConfidence);
                existing.captureId = pin.captureId ?? existing.captureId;
                existing.captureSessionId = pin.captureSessionId ?? existing.captureSessionId;
                existing.captureFrameIndex = pin.captureFrameIndex != 0 ? pin.captureFrameIndex : existing.captureFrameIndex;
                existing.hitType = pin.hitType;

                _pins[i] = existing;
                UpdateInstance(i, existing);
                return true;
            }

            return false;
        }

        private void PruneStalePins(double now)
        {
            if (pinMaxAgeSeconds <= 0f)
            {
                return;
            }

            for (int i = _pins.Count - 1; i >= 0; i--)
            {
                if ((now - _pins[i].timestamp) <= pinMaxAgeSeconds)
                {
                    continue;
                }

                if (i < _instances.Count && _instances[i] != null)
                {
                    Destroy(_instances[i]);
                }

                _pins.RemoveAt(i);
                if (i < _instances.Count)
                {
                    _instances.RemoveAt(i);
                }
            }
        }

        private void UpdateInstance(int index, SpatialPin pin)
        {
            if (index < 0 || index >= _instances.Count)
            {
                return;
            }

            var instance = _instances[index];
            if (instance == null)
            {
                return;
            }

            instance.transform.position = pin.position;
            if (pin.rotation != Quaternion.identity)
            {
                instance.transform.rotation = pin.rotation;
            }

            var text = instance.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = pin.label;
            }
        }

        private void SpawnPin(SpatialPin pin)
        {
            if (pinPrefab == null)
            {
                _instances.Add(null); // keep indices aligned
                return;
            }

            Transform parent = pinRoot != null ? pinRoot : transform;
            var instance = Instantiate(pinPrefab, parent);
            instance.transform.position = pin.position;
            instance.transform.localScale = pinScale;

            if (pin.rotation != Quaternion.identity)
            {
                instance.transform.rotation = pin.rotation;
            }

            var text = instance.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = pin.label;
            }

            _instances.Add(instance);
        }
    }
}

