using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace CompanionScan.Mapping
{
    /// <summary>
    /// Central store for <see cref="SpatialPin"/>s from both capture systems.
    /// Spawns a world-space pin prefab for each pin.
    /// </summary>
    /// <remarks>
    /// Pins stay until <see cref="ClearPins()"/> removes them or a newer pin of the same object
    /// replaces them: the same Space Setup anchor, the same track, or (with deduplication) the same
    /// label within <c>dedupDistance</c>. Dropping pins by age (<c>enablePinLifecycle</c>) is
    /// optional and off by default: with it on, every capture removed the pins of earlier captures,
    /// while labels are documented to persist across captures.
    /// </remarks>
    public class SpatialPinManager : MonoBehaviour
    {
        [SerializeField] private Transform pinRoot;
        [SerializeField] private GameObject pinPrefab;
        [SerializeField] private Vector3 pinScale = Vector3.one;
        [SerializeField] private bool enableDeduplication = true;
        [SerializeField] private float dedupDistance = 0.15f;
        [Tooltip("Optional: when a pin arrives, drop the pins more than pinMaxAgeSeconds older. Off by default, " +
                 "so pins stay until they are cleared or replaced by a newer pin of the same object.")]
        [SerializeField] private bool enablePinLifecycle = false;
        [SerializeField] private float pinMaxAgeSeconds = 6f;
        [SerializeField] private bool preferTrackIds = true;

        private readonly List<SpatialPin> _pins = new List<SpatialPin>();
        private readonly List<GameObject> _instances = new List<GameObject>();
        private bool _pinsVisible = true;
        private bool _warnedNoPrefab;

        /// <summary>Raised after pins were added, updated, replaced, removed or cleared.</summary>
        public event Action PinsChanged;

        /// <summary>All pins from every source.</summary>
        public IReadOnlyList<SpatialPin> Pins => _pins;

        /// <summary>The object shown for each pin, at the same index as <see cref="Pins"/> (null without a prefab).</summary>
        public IReadOnlyList<GameObject> PinObjects => _instances;

        /// <summary>The root transform under which pin instances are spawned.</summary>
        public Transform PinRoot => pinRoot != null ? pinRoot : transform;

        /// <summary>
        /// Shows or hides the pin objects. Only those objects are switched, never this component's
        /// GameObject: in the shipped scene that object also runs capture.
        /// </summary>
        public bool PinsVisible
        {
            get => _pinsVisible;
            set
            {
                if (_pinsVisible == value)
                {
                    return;
                }

                _pinsVisible = value;
                for (int i = 0; i < _instances.Count; i++)
                {
                    if (_instances[i] != null && _instances[i].activeSelf != value)
                    {
                        _instances[i].SetActive(value);
                    }
                }
            }
        }

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
                DestroyPinObject(_instances[i]);
            }
            _instances.Clear();
            _pins.Clear();
            PinsChanged?.Invoke();
        }

        /// <summary>Remove only pins from a specific source.</summary>
        public void ClearPins(PinSource source)
        {
            for (int i = _pins.Count - 1; i >= 0; i--)
            {
                if (_pins[i].source == source)
                {
                    RemovePinAt(i);
                }
            }
            PinsChanged?.Invoke();
        }

        public void AddPin(SpatialPin pin)
        {
            if (pin == null)
            {
                return;
            }

            if (enablePinLifecycle)
            {
                PruneStalePins(pin.timestamp);
            }

            bool replaced = TryReplaceAnchor(pin)
                            || (preferTrackIds && pin.trackId != 0 && TryMergeTrack(pin))
                            || (enableDeduplication && TryMergePin(pin));
            if (!replaced)
            {
                _pins.Add(pin);
                SpawnPin(pin);
            }

            PinsChanged?.Invoke();
        }

        /// <summary>
        /// A newer reading of the same Space Setup anchor replaces its pin: the room may have been
        /// set up again since, with the object moved or resized.
        /// </summary>
        private bool TryReplaceAnchor(SpatialPin pin)
        {
            if (string.IsNullOrEmpty(pin.anchorUuid))
            {
                return false;
            }

            for (int i = 0; i < _pins.Count; i++)
            {
                var existing = _pins[i];
                if (existing.source != pin.source ||
                    !string.Equals(existing.anchorUuid, pin.anchorUuid, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _pins[i] = pin;
                UpdateInstance(i, pin);
                return true;
            }

            return false;
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

                // Two different Space Setup anchors are two objects, however close.
                if (!string.IsNullOrEmpty(existing.anchorUuid) && !string.IsNullOrEmpty(pin.anchorUuid))
                {
                    continue;
                }

                // "OTHER" covers every unmapped class: a cup next to a book is two objects.
                if (string.Equals(pin.semanticLabel, "OTHER", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(existing.label, pin.label, StringComparison.OrdinalIgnoreCase))
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

                RemovePinAt(i);
            }
        }

        /// <summary>Removes a pin and its object; the two lists stay index-aligned.</summary>
        private void RemovePinAt(int index)
        {
            if (index < _instances.Count)
            {
                DestroyPinObject(_instances[index]);
                _instances.RemoveAt(index);
            }

            _pins.RemoveAt(index);
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
            SetPinText(instance, pin.label);
        }

        // The pin object stands upright at the pin's position. The pin's rotation (a Space Setup
        // anchor's orientation, which for floors and furniture points +Y sideways) stays in the data.
        private void SpawnPin(SpatialPin pin)
        {
            if (pinPrefab == null)
            {
                if (!_warnedNoPrefab)
                {
                    _warnedNoPrefab = true;
                    Debug.LogWarning("[Pins] No pin prefab is set, so pins are kept and exported but not shown.", this);
                }

                _instances.Add(null); // keep indices aligned
                return;
            }

            var instance = Instantiate(pinPrefab, PinRoot);
            instance.transform.position = pin.position;
            instance.transform.localScale = pinScale;
            SetPinText(instance, pin.label);
            if (!_pinsVisible)
            {
                instance.SetActive(false);
            }

            _instances.Add(instance);
        }

        private static void SetPinText(GameObject instance, string label)
        {
            var text = instance.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
            }
        }

        private static void DestroyPinObject(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }
    }
}

