using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CompanionScan.UI
{
    /// <summary>
    /// A world-space text panel built at run time and kept in front of the viewer: the layout the
    /// settings panel, the camera notice and the licences view share. The panel is a scene root,
    /// not a child of its owner, because it is placed in world space and must not follow its host.
    /// </summary>
    internal sealed class HeadsetPanel
    {
        private const float RecenterAngleDegrees = 35f;
        private const float Inset = 24f;

        private readonly float _width;
        private readonly float _distance;
        private readonly Transform _layout;

        public HeadsetPanel(string name, float width, float height, float scale, float distance, Color background)
        {
            _width = width;
            _distance = distance;

            Root = new GameObject(name);
            Root.transform.localScale = Vector3.one * scale;

            var canvas = Root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            Root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            Root.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);

            var backgroundObject = new GameObject("Background", typeof(RectTransform));
            backgroundObject.transform.SetParent(Root.transform, false);
            Stretch(backgroundObject.GetComponent<RectTransform>(), 0f);
            backgroundObject.AddComponent<Image>().color = background;

            var layout = new GameObject("Layout", typeof(RectTransform));
            layout.transform.SetParent(backgroundObject.transform, false);
            Stretch(layout.GetComponent<RectTransform>(), Inset);
            var group = layout.AddComponent<VerticalLayoutGroup>();
            group.spacing = 14f;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childControlWidth = true;
            group.childControlHeight = false;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            _layout = layout.transform;

            Root.SetActive(false);
        }

        public GameObject Root { get; }

        public bool IsVisible => Root != null && Root.activeSelf;

        /// <summary>
        /// Adds a text row of fixed height. Rows without wrapping end in an ellipsis when too long;
        /// wrapped rows (several lines) are top-aligned.
        /// </summary>
        public TMP_Text AddText(string name, string text, float size, float height, FontStyles style, Color color,
            bool wrap = false, bool richText = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_layout, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(_width - 2f * Inset, height);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.richText = richText;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = wrap ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        public void Show()
        {
            PlaceInFrontOfViewer();
            Root.SetActive(true);
        }

        public void Hide()
        {
            if (Root != null)
            {
                Root.SetActive(false);
            }
        }

        public void Destroy()
        {
            if (Root != null)
            {
                Object.Destroy(Root);
            }
        }

        /// <summary>
        /// Re-centers the panel when it drifts out of view, e.g. when it was opened before head
        /// tracking settled or the user turned away.
        /// </summary>
        public void KeepInView()
        {
            if (!IsVisible)
            {
                return;
            }

            var viewer = Camera.main != null ? Camera.main.transform : null;
            if (viewer == null)
            {
                return;
            }

            // Compare headings only, so looking down at the controllers does not move the panel.
            var flatForward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 1e-4f)
            {
                return;
            }

            var toPanel = Root.transform.position - viewer.position;
            var flatToPanel = Vector3.ProjectOnPlane(toPanel, Vector3.up);
            bool misplaced = toPanel.magnitude > _distance * 2f || Mathf.Abs(toPanel.y) > _distance * 0.5f;
            bool outOfView = flatToPanel.sqrMagnitude > 1e-4f &&
                             Vector3.Angle(flatForward, flatToPanel) > RecenterAngleDegrees;
            if (misplaced || outOfView)
            {
                PlaceInFrontOfViewer();
            }
        }

        public void PlaceInFrontOfViewer()
        {
            var viewer = Camera.main != null ? Camera.main.transform : null;
            if (viewer == null || Root == null)
            {
                return;
            }

            var forward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = viewer.forward;
            }

            forward.Normalize();
            Root.transform.SetPositionAndRotation(
                viewer.position + forward * _distance,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
