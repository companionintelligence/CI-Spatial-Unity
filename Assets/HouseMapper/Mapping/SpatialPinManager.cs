using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace HouseMapper.Mapping
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

        private readonly List<SpatialPin> _pins = new List<SpatialPin>();
        private readonly List<GameObject> _instances = new List<GameObject>();

        /// <summary>All pins from every source.</summary>
        public IReadOnlyList<SpatialPin> Pins => _pins;

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
            _pins.Add(pin);
            SpawnPin(pin);
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
