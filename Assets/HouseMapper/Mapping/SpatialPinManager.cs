using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HouseMapper.Mapping
{
    public class SpatialPinManager : MonoBehaviour
    {
        [SerializeField] private Transform pinRoot;
        [SerializeField] private GameObject pinPrefab;
        [SerializeField] private Vector3 pinScale = Vector3.one;

        private readonly List<SpatialPin> _pins = new List<SpatialPin>();
        private readonly List<GameObject> _instances = new List<GameObject>();

        public IReadOnlyList<SpatialPin> Pins => _pins;

        public void ClearPins()
        {
            for (int i = 0; i < _instances.Count; i++)
            {
                if (_instances[i] != null)
                {
                    Destroy(_instances[i]);
                }
            }
            _instances.Clear();
            _pins.Clear();
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
                return;
            }

            Transform parent = pinRoot != null ? pinRoot : transform;
            var instance = Instantiate(pinPrefab, parent);
            instance.transform.position = pin.position;
            instance.transform.localScale = pinScale;

            var text = instance.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = pin.label;
            }

            _instances.Add(instance);
        }
    }
}
