using System.Collections.Generic;
using CompanionScan.Mapping;
using TMPro;
using UnityEngine;

namespace CompanionScan.Rendering
{
    /// <summary>
    /// Floats a 3D label beside every pin of a <see cref="SpatialPinManager"/>: vision server
    /// detections and Space Setup objects alike. A label follows its pin: it moves when a newer
    /// observation of the same object moves the pin, and goes when the pin is cleared, so a pin
    /// never collects more than one label. The label stays where it is put; the prefab's
    /// <see cref="SpatialLabelView"/> only turns it to face the viewer.
    /// <see cref="RenderLabels"/> still places free-standing labels at given points.
    /// </summary>
    public class LabelRenderer : MonoBehaviour
    {
        [SerializeField] private Transform labelRoot;
        [SerializeField] private GameObject labelPrefab;

        /// <summary>
        /// When true, previous labels are kept when new labels are rendered.
        /// Set to false and call <see cref="Clear"/> to replicate the old
        /// clear-on-render behaviour.
        /// </summary>
        [SerializeField] private bool persistLabels = true;

        [Tooltip("The pins to label. SpatialCapturePipeline connects its own pin manager when this is empty.")]
        [SerializeField] private SpatialPinManager pinManager;

        [Tooltip("Where a label floats relative to its point, in metres: on top of the pin's stem.")]
        [SerializeField] private Vector3 labelOffset = new Vector3(0f, 0.12f, 0f);

        private readonly List<GameObject> _instances = new List<GameObject>();
        private readonly Dictionary<SpatialPin, GameObject> _pinLabels = new Dictionary<SpatialPin, GameObject>();
        private readonly HashSet<SpatialPin> _livePins = new HashSet<SpatialPin>();
        private readonly List<SpatialPin> _gonePins = new List<SpatialPin>();
        private SpatialPinManager _subscribedTo;
        private bool _visible = true;
        private bool _warnedNoPrefab;

        /// <summary>Where labels are created: <c>labelRoot</c>, or this object when it is not set.</summary>
        public Transform LabelRoot => labelRoot != null ? labelRoot : transform;

        /// <summary>The pin store whose pins get labels, or null.</summary>
        public SpatialPinManager PinManager => pinManager;

        /// <summary>Where a label floats relative to its pin.</summary>
        public Vector3 LabelOffset => labelOffset;

        /// <summary>Number of labels that follow pins.</summary>
        public int PinLabelCount => _pinLabels.Count;

        /// <summary>
        /// Shows or hides every label. Only the label objects are switched, never this component's
        /// GameObject: in the shipped scene that object also runs capture.
        /// </summary>
        public bool Visible
        {
            get => _visible;
            set
            {
                if (_visible == value)
                {
                    return;
                }

                _visible = value;
                foreach (var label in _instances)
                {
                    ApplyVisibility(label);
                }

                foreach (var label in _pinLabels.Values)
                {
                    ApplyVisibility(label);
                }
            }
        }

        /// <summary>The label that follows <paramref name="pin"/>, if there is one.</summary>
        public bool TryGetLabel(SpatialPin pin, out GameObject label)
        {
            label = null;
            return pin != null && _pinLabels.TryGetValue(pin, out label) && label != null;
        }

        /// <summary>Labels the pins of <paramref name="manager"/> from now on; null stops following.</summary>
        public void Follow(SpatialPinManager manager)
        {
            Unsubscribe();
            pinManager = manager;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            SyncToPins();
        }

        private void OnEnable()
        {
            Subscribe();
            SyncToPins();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (pinManager == null || ReferenceEquals(_subscribedTo, pinManager))
            {
                return;
            }

            Unsubscribe();
            pinManager.PinsChanged += SyncToPins;
            _subscribedTo = pinManager;
        }

        private void Unsubscribe()
        {
            if (!ReferenceEquals(_subscribedTo, null))
            {
                _subscribedTo.PinsChanged -= SyncToPins;
            }

            _subscribedTo = null;
        }

        /// <summary>
        /// Brings the labels in line with the pins: one label per pin, beside it, with its text.
        /// Runs whenever the pin manager reports a change.
        /// </summary>
        public void SyncToPins()
        {
            _livePins.Clear();
            var pins = pinManager != null ? pinManager.Pins : null;
            if (pins != null)
            {
                for (int i = 0; i < pins.Count; i++)
                {
                    var pin = pins[i];
                    if (pin == null || !_livePins.Add(pin))
                    {
                        continue;
                    }

                    if (!_pinLabels.TryGetValue(pin, out var label) || label == null)
                    {
                        label = Spawn();
                        if (label == null)
                        {
                            _pinLabels.Remove(pin);
                            continue;
                        }

                        _pinLabels[pin] = label;
                    }

                    Place(label, pin.position, pin.label);
                }
            }

            _gonePins.Clear();
            foreach (var entry in _pinLabels)
            {
                if (!_livePins.Contains(entry.Key))
                {
                    _gonePins.Add(entry.Key);
                }
            }

            foreach (var pin in _gonePins)
            {
                DestroyLabel(_pinLabels[pin]);
                _pinLabels.Remove(pin);
            }
        }

        /// <summary>Places a free-standing label beside each of <paramref name="positions"/>.</summary>
        public void RenderLabels(IReadOnlyList<Vector3> positions, IReadOnlyList<string> labels)
        {
            // Only clear old labels when persistence is disabled
            if (!persistLabels)
                Clear();

            if (positions == null || labels == null)
            {
                return;
            }

            int count = Mathf.Min(positions.Count, labels.Count);
            for (int i = 0; i < count; i++)
            {
                var labelObj = Spawn();
                if (labelObj == null)
                {
                    return;
                }

                Place(labelObj, positions[i], labels[i]);
                _instances.Add(labelObj);
            }
        }

        /// <summary>Removes the labels placed by <see cref="RenderLabels"/>. Pin labels go with their pins.</summary>
        public void Clear()
        {
            for (int i = 0; i < _instances.Count; i++)
            {
                DestroyLabel(_instances[i]);
            }
            _instances.Clear();
        }

        private GameObject Spawn()
        {
            if (labelPrefab == null)
            {
                if (!_warnedNoPrefab)
                {
                    _warnedNoPrefab = true;
                    Debug.LogWarning("[Labels] No label prefab is set, so no 3D labels are shown.", this);
                }

                return null;
            }

            var label = Instantiate(labelPrefab, LabelRoot);
            if (!_visible)
            {
                label.SetActive(false);
            }

            return label;
        }

        private void Place(GameObject label, Vector3 point, string text)
        {
            label.transform.position = point + labelOffset;

            var view = label.GetComponent<SpatialLabelView>();
            if (view != null)
            {
                view.SetText(text);
                return;
            }

            var tmp = label.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = text;
            }
        }

        private void ApplyVisibility(GameObject label)
        {
            if (label != null && label.activeSelf != _visible)
            {
                label.SetActive(_visible);
            }
        }

        private static void DestroyLabel(GameObject label)
        {
            if (label == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(label);
            }
            else
            {
                DestroyImmediate(label);
            }
        }
    }
}

