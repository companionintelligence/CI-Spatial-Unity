using System.Collections.Generic;

namespace Companion.Spatial.Extras
{
    /// <summary>
    /// The extras block of a scan, schema v2: what the scanner writes into the root <c>extras</c> of the glTF it
    /// exports, and what the web Scan app reads and writes too. It holds the room (<see cref="Space"/>), the user's
    /// consent, the device, the camera frames that were kept, the objects found in the room, the surfaces of the headset's
    /// scene model, and the bounds of it all. <see cref="ExtrasReader"/> reads v1 and v2 into this model,
    /// <see cref="ExtrasWriter"/> writes it, <see cref="ExtrasMerge"/> merges a new export into an earlier one.
    /// </summary>
    public sealed class ScanExtras : ExtrasNode
    {
        /// <summary>The schema id, written as <c>schema</c>.</summary>
        public const string SchemaId = "companion.scan-extras";

        /// <summary>The schema version this model writes, 2 (written as <c>schema_version</c>; v1 had the text "1.0").</summary>
        public const int CurrentVersion = 2;

        /// <summary>Creates an empty document: the default space, no consent or device, no frames, objects or planes.</summary>
        public ScanExtras()
        {
            Space = new ScanSpace();
            Frames = new List<ScanFrame>();
            Objects = new List<ScanObject>();
            Planes = new List<ScanPlane>();
        }

        /// <summary>The room the file describes (JSON <c>space</c>).</summary>
        public ScanSpace Space { get; set; }

        /// <summary>The user's consent, or null when none is recorded (JSON <c>consent</c>).</summary>
        public ScanConsent Consent { get; set; }

        /// <summary>The device and app that made the scan, or null when unknown (JSON <c>device</c>).</summary>
        public ScanDevice Device { get; set; }

        /// <summary>The camera frames the scan kept; never null (JSON <c>frames</c>).</summary>
        public List<ScanFrame> Frames { get; }

        /// <summary>The objects found in the room; never null (JSON <c>objects</c>).</summary>
        public List<ScanObject> Objects { get; }

        /// <summary>The surfaces of the headset's scene model; never null (JSON <c>planes</c>).</summary>
        public List<ScanPlane> Planes { get; }

        /// <summary>The box around everything in the file, or null for an empty one (JSON <c>bounds</c>).</summary>
        public ScanBounds Bounds { get; set; }

        /// <summary>
        /// The axis-aligned box around the content: the position of every object and the extent of those that have
        /// one, and the outline of every plane (its position when it has no outline). Points that are not finite are
        /// ignored. Null when there is nothing to enclose. The document itself is not changed; assign the result to
        /// <see cref="Bounds"/> to keep it.
        /// </summary>
        public ScanBounds ComputeBounds()
        {
            bool any = false;
            Vec3 low = Vec3.Zero;
            Vec3 high = Vec3.Zero;

            foreach (ScanObject obj in Objects)
            {
                if (obj == null)
                {
                    continue;
                }

                if (obj.Pose != null)
                {
                    Include(obj.Pose.Position, ref any, ref low, ref high);
                }

                if (obj.Bounds != null)
                {
                    Include(obj.Bounds.Min, ref any, ref low, ref high);
                    Include(obj.Bounds.Max, ref any, ref low, ref high);
                }
            }

            foreach (ScanPlane plane in Planes)
            {
                if (plane == null)
                {
                    continue;
                }

                if (plane.Boundary.Count > 0)
                {
                    foreach (Vec3 point in plane.Boundary)
                    {
                        Include(point, ref any, ref low, ref high);
                    }
                }
                else if (plane.Pose != null)
                {
                    Include(plane.Pose.Position, ref any, ref low, ref high);
                }
            }

            return any ? ScanBounds.FromMinMax(low, high) : null;
        }

        private static void Include(Vec3 point, ref bool any, ref Vec3 low, ref Vec3 high)
        {
            if (!point.IsFinite)
            {
                return;
            }

            if (!any)
            {
                low = point;
                high = point;
                any = true;
                return;
            }

            low = Vec3.Min(low, point);
            high = Vec3.Max(high, point);
        }
    }
}
