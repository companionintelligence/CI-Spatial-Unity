using TMPro;
using UnityEngine;

namespace CompanionScan.Rendering
{
    /// <summary>
    /// A 3D label: TextMesh Pro text on a dark plate. <see cref="LabelRenderer"/> puts it beside a
    /// pin and leaves it there, so it stays pinned in the room; this component only turns it about
    /// the vertical axis to face the viewer, so the text stays upright and readable from wherever
    /// the user stands. In the prefab the text and plate sit a few centimetres toward the viewer,
    /// so a label does not sink into the wall or object its pin is on.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpatialLabelView : MonoBehaviour
    {
        /// <summary>Longer labels are cut to this many characters, so one label cannot fill the room.</summary>
        public const int MaxLength = 48;

        [SerializeField] private TMP_Text text;

        [Tooltip("Backing quad, resized to the text plus the padding.")]
        [SerializeField] private Transform plate;

        [SerializeField] private Vector2 platePadding = new Vector2(0.016f, 0.008f);

        [Tooltip("Turn about the vertical axis to face the main camera every frame.")]
        [SerializeField] private bool faceViewer = true;

        private string _text = string.Empty;
        private bool _plateDirty = true;

        /// <summary>The text the label shows.</summary>
        public string Text => _text;

        /// <summary>Shows <paramref name="value"/> as plain text, cut to <see cref="MaxLength"/> characters.</summary>
        public void SetText(string value)
        {
            value = Shorten(value);
            if (value == _text && (text == null || text.text == value))
            {
                return;
            }

            _text = value;
            if (text != null)
            {
                text.text = value;
            }

            _plateDirty = true;
            if (isActiveAndEnabled)
            {
                FitPlate();
            }
        }

        /// <summary>Turns the label about the vertical axis so its text faces <paramref name="viewer"/>.</summary>
        public void FaceViewer(Transform viewer)
        {
            if (viewer == null)
            {
                return;
            }

            // TextMesh Pro text reads from its -Z side, so point +Z away from the viewer.
            var away = transform.position - viewer.position;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-6f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }

        private static string Shorten(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            value = value.Trim();
            return value.Length <= MaxLength ? value : value.Substring(0, MaxLength - 3).TrimEnd() + "...";
        }

        private void OnEnable()
        {
            _plateDirty = true;
        }

        private void LateUpdate()
        {
            if (_plateDirty)
            {
                FitPlate();
            }

            if (faceViewer)
            {
                var viewer = Camera.main;
                if (viewer != null)
                {
                    FaceViewer(viewer.transform);
                }
            }
        }

        private void FitPlate()
        {
            _plateDirty = false;
            if (text == null || plate == null)
            {
                return;
            }

            text.ForceMeshUpdate();
            var bounds = text.textBounds;
            bool hasText = !string.IsNullOrEmpty(_text) && bounds.size.x > 0f && bounds.size.y > 0f;
            if (plate.gameObject.activeSelf != hasText)
            {
                plate.gameObject.SetActive(hasText);
            }

            if (!hasText)
            {
                return;
            }

            // The text is an unrotated child: its bounds map into this object's space by its own
            // position and scale.
            var textTransform = text.transform;
            var center = textTransform.localPosition + Vector3.Scale(textTransform.localScale, bounds.center);
            var size = Vector3.Scale(textTransform.localScale, bounds.size);
            plate.localPosition = new Vector3(center.x, center.y, plate.localPosition.z);
            plate.localScale = new Vector3(size.x + 2f * platePadding.x, size.y + 2f * platePadding.y, 1f);
        }
    }
}
