using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HouseMapper.Rendering
{
    public class LabelRenderer : MonoBehaviour
    {
        [SerializeField] private Transform labelRoot;
        [SerializeField] private GameObject labelPrefab;

        private readonly List<GameObject> _instances = new List<GameObject>();

        public void RenderLabels(IReadOnlyList<Vector3> positions, IReadOnlyList<string> labels)
        {
            Clear();
            if (labelPrefab == null || labelRoot == null)
            {
                return;
            }

            int count = Mathf.Min(positions.Count, labels.Count);
            for (int i = 0; i < count; i++)
            {
                var labelObj = Instantiate(labelPrefab, labelRoot);
                labelObj.transform.position = positions[i];

                var text = labelObj.GetComponentInChildren<TMP_Text>();
                if (text != null)
                {
                    text.text = labels[i];
                }

                _instances.Add(labelObj);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _instances.Count; i++)
            {
                if (_instances[i] != null)
                {
                    Destroy(_instances[i]);
                }
            }
            _instances.Clear();
        }
    }
}
