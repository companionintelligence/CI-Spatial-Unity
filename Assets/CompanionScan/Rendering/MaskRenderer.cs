using System.Collections;
using System.Collections.Generic;
using CompanionScan.Core;
using CompanionScan.Utils;
using UnityEngine;

namespace CompanionScan.Rendering
{
    public class MaskRenderer : MonoBehaviour
    {
        [SerializeField] private Transform maskRoot;
        [SerializeField] private Material maskMaterial;
        [SerializeField] private float depthOffset = 1.0f;
        [SerializeField] private Color[] palette;

        private readonly List<GameObject> _instances = new List<GameObject>();

        public void RenderMasks(SegmentationResult result)
        {
            Clear();
            if (result == null || result.annotations == null)
            {
                return;
            }

            for (int i = 0; i < result.annotations.Count; i++)
            {
                var annotation = result.annotations[i];
                if (annotation.mask == null)
                {
                    continue;
                }

                var color = palette != null && palette.Length > 0
                    ? palette[i % palette.Length]
                    : new Color(0.2f, 0.8f, 0.2f, 0.4f);
                color.a = Mathf.Clamp01(color.a);

                var maskTex = RleDecoder.DecodeToTexture(annotation.mask, color);
                if (maskTex == null)
                {
                    continue;
                }

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.transform.SetParent(maskRoot, false);
                quad.transform.localPosition = new Vector3(0f, 0f, depthOffset);

                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.material = new Material(maskMaterial != null ? maskMaterial : renderer.material);
                renderer.material.mainTexture = maskTex;
                renderer.material.color = color;

                _instances.Add(quad);
            }
        }

        public void RenderMasksAsync(SegmentationResult result, int masksPerFrame = 1)
        {
            StopAllCoroutines();
            StartCoroutine(RenderRoutine(result, Mathf.Max(1, masksPerFrame)));
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

        private IEnumerator RenderRoutine(SegmentationResult result, int masksPerFrame)
        {
            Clear();
            if (result == null || result.annotations == null)
            {
                yield break;
            }

            int processed = 0;
            for (int i = 0; i < result.annotations.Count; i++)
            {
                var annotation = result.annotations[i];
                if (annotation.mask == null)
                {
                    continue;
                }

                var color = palette != null && palette.Length > 0
                    ? palette[i % palette.Length]
                    : new Color(0.2f, 0.8f, 0.2f, 0.4f);
                color.a = Mathf.Clamp01(color.a);

                var maskTex = RleDecoder.DecodeToTexture(annotation.mask, color);
                if (maskTex == null)
                {
                    continue;
                }

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.transform.SetParent(maskRoot, false);
                quad.transform.localPosition = new Vector3(0f, 0f, depthOffset);

                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.material = new Material(maskMaterial != null ? maskMaterial : renderer.material);
                renderer.material.mainTexture = maskTex;
                renderer.material.color = color;

                _instances.Add(quad);

                processed++;
                if (processed >= masksPerFrame)
                {
                    processed = 0;
                    yield return null;
                }
            }
        }
    }
}

