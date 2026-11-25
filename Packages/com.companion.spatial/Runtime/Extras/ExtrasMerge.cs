using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Companion.Spatial.Extras
{
    /// <summary>
    /// Merges a new export into the document an earlier export of the same space left in the glTF, by object id. It
    /// replaces the scanner's <c>MergeExtras</c>, which appended: a re-export listed every pin twice, kept the old
    /// timestamp and bounds, wrote <c>"pins": null</c>, and a glTF cut short made every later export throw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Objects and frames are merged by <c>id</c>: an id the new export has replaces the earlier entry in its place,
    /// an id only the earlier document has is kept, a new id is appended. Entries with no id cannot be matched and are
    /// all kept, with a warning. The planes are one snapshot of the headset's scene: a new export that has planes
    /// replaces all the earlier ones (Space Setup may have been done again), one that has none keeps them.
    /// </para>
    /// <para>
    /// The time of the file is recomputed: <c>created_at_utc</c> is the earliest time known (an earlier v1 file only
    /// knew its last export, which counts), <c>updated_at_utc</c> the latest; times are compared as instants and a
    /// missing time is never made up. The bounds are recomputed from the merged content (and cover the new export's
    /// own bounds, which may include a room outline the objects do not), not kept from the earlier file.
    /// </para>
    /// <para>
    /// Consent belongs to the export that recorded it and is never carried over: the merged document has the new
    /// export's consent, or none (with a warning when the earlier document had one). The device is the new export's,
    /// else the earlier one's. Unknown keys of both are kept, the new export's winning a clash.
    /// </para>
    /// <para>
    /// An earlier document that cannot be used is replaced by the new export, with a warning and
    /// <see cref="ExtrasMergeResult.ExistingDiscarded"/> set, never an exception: one that is corrupt or not a scan's
    /// extras, one for another space (ids that differ), one in another coordinate frame. An earlier v1 file is
    /// upgraded and merged like any other. Object ids must be unique across app runs for this to work (see
    /// <see cref="ScanObject.MakeId"/>).
    /// </para>
    /// </remarks>
    public static class ExtrasMerge
    {
        /// <summary>Merges a new export into an earlier document.</summary>
        /// <param name="existing">The earlier document, or null for a first export.</param>
        /// <param name="incoming">The new export.</param>
        /// <returns>The merged document and the warnings. The inputs are not changed, but the result shares their objects, frames and planes.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="incoming"/> is null.</exception>
        public static ExtrasMergeResult Merge(ScanExtras existing, ScanExtras incoming)
        {
            if (incoming == null)
            {
                throw new ArgumentNullException(nameof(incoming));
            }

            return Combine(existing, incoming, new List<string>(), false);
        }

        /// <summary>
        /// Merges a new export into an earlier document given as JSON text (the <c>extras</c> of the glTF the last export
        /// wrote). Null, empty or blank text means there is none; text that cannot be read is replaced, with a warning.
        /// </summary>
        /// <param name="existingJson">The earlier extras as JSON text, or null.</param>
        /// <param name="incoming">The new export.</param>
        /// <exception cref="ArgumentNullException"><paramref name="incoming"/> is null.</exception>
        public static ExtrasMergeResult MergeFromText(string existingJson, ScanExtras incoming)
        {
            if (incoming == null)
            {
                throw new ArgumentNullException(nameof(incoming));
            }

            if (string.IsNullOrWhiteSpace(existingJson))
            {
                return Combine(null, incoming, new List<string>(), false);
            }

            return FromRead(ExtrasReader.Read(existingJson), incoming);
        }

        /// <summary>
        /// Merges a new export into an earlier document given as a JSON object (the <c>extras</c> of the glTF the last
        /// export wrote). Null means there is none; an object that is not a scan's extras is replaced, with a warning.
        /// </summary>
        /// <param name="existing">The earlier extras, or null.</param>
        /// <param name="incoming">The new export.</param>
        /// <exception cref="ArgumentNullException"><paramref name="incoming"/> is null.</exception>
        public static ExtrasMergeResult MergeFromObject(JObject existing, ScanExtras incoming)
        {
            if (incoming == null)
            {
                throw new ArgumentNullException(nameof(incoming));
            }

            if (existing == null)
            {
                return Combine(null, incoming, new List<string>(), false);
            }

            return FromRead(ExtrasReader.Read(existing), incoming);
        }

        private static ExtrasMergeResult FromRead(ExtrasReadResult read, ScanExtras incoming)
        {
            var warnings = new List<string>();
            if (!read.Success)
            {
                warnings.Add("The earlier document could not be read (" + read.Error + "); it was replaced by the new export.");
                return Combine(null, incoming, warnings, true);
            }

            foreach (string warning in read.Warnings)
            {
                warnings.Add("earlier document: " + warning);
            }

            return Combine(read.Extras, incoming, warnings, false);
        }

        private static ExtrasMergeResult Combine(ScanExtras existing, ScanExtras incoming, List<string> warnings, bool discarded)
        {
            if (existing != null && !SameSpaceAndFrame(existing, incoming, warnings))
            {
                existing = null;
                discarded = true;
            }

            var merged = new ScanExtras();
            merged.Space = MergeSpace(existing == null ? null : existing.Space, incoming.Space);

            merged.Consent = incoming.Consent;
            if (incoming.Consent == null && existing != null && existing.Consent != null)
            {
                warnings.Add("The new export carries no consent record, so the merged document has none: consent recorded for the earlier data is not carried over.");
            }

            merged.Device = incoming.Device ?? (existing == null ? null : existing.Device);

            int withoutId = 0;
            var frames = new Dictionary<string, int>(StringComparer.Ordinal);
            if (existing != null)
            {
                Upsert(merged.Frames, frames, existing.Frames, frame => frame.Id, ref withoutId);
            }

            Upsert(merged.Frames, frames, incoming.Frames, frame => frame.Id, ref withoutId);
            WarnAboutMissingIds(warnings, "frame", ref withoutId);

            var objects = new Dictionary<string, int>(StringComparer.Ordinal);
            if (existing != null)
            {
                Upsert(merged.Objects, objects, existing.Objects, obj => obj.Id, ref withoutId);
            }

            Upsert(merged.Objects, objects, incoming.Objects, obj => obj.Id, ref withoutId);
            WarnAboutMissingIds(warnings, "object", ref withoutId);

            // the planes are one snapshot of the scene: the newest one that has any
            var planes = new Dictionary<string, int>(StringComparer.Ordinal);
            if (HasAny(incoming.Planes) || existing == null)
            {
                Upsert(merged.Planes, planes, incoming.Planes, plane => plane.Id, ref withoutId);
            }
            else
            {
                Upsert(merged.Planes, planes, existing.Planes, plane => plane.Id, ref withoutId);
            }

            WarnAboutMissingIds(warnings, "plane", ref withoutId);

            if (existing != null)
            {
                OverlayUnknown(merged.Unknown, existing.Unknown);
            }

            OverlayUnknown(merged.Unknown, incoming.Unknown);
            merged.Bounds = Union(merged.ComputeBounds(), incoming.Bounds);
            return new ExtrasMergeResult(merged, warnings, discarded);
        }

        // Two documents can only be merged when they describe the same space in the same coordinate frame.
        private static bool SameSpaceAndFrame(ScanExtras existing, ScanExtras incoming, List<string> warnings)
        {
            string oldId = existing.Space == null ? null : existing.Space.Id;
            string newId = incoming.Space == null ? null : incoming.Space.Id;
            if (!string.IsNullOrEmpty(oldId) && !string.IsNullOrEmpty(newId) && !string.Equals(oldId, newId, StringComparison.Ordinal))
            {
                warnings.Add("The earlier document is for the space \"" + oldId + "\" and the new export for \"" + newId + "\"; the earlier one was replaced, not merged.");
                return false;
            }

            string oldFrame = existing.Space == null ? null : existing.Space.Frame;
            string newFrame = incoming.Space == null ? null : incoming.Space.Frame;
            if (!string.IsNullOrEmpty(oldFrame) && !string.IsNullOrEmpty(newFrame) && !string.Equals(oldFrame, newFrame, StringComparison.Ordinal))
            {
                warnings.Add("The earlier document is in the coordinate frame \"" + oldFrame + "\" and the new export in \"" + newFrame + "\"; the earlier one was replaced, not merged.");
                return false;
            }

            return true;
        }

        private static ScanSpace MergeSpace(ScanSpace existing, ScanSpace incoming)
        {
            var space = new ScanSpace();
            string id = incoming == null ? null : incoming.Id;
            space.Id = !string.IsNullOrEmpty(id) ? id : (existing != null && !string.IsNullOrEmpty(existing.Id) ? existing.Id : ScanSpace.DefaultId);

            string frame = incoming == null ? null : incoming.Frame;
            space.Frame = !string.IsNullOrEmpty(frame) ? frame : (existing != null && !string.IsNullOrEmpty(existing.Frame) ? existing.Frame : ScanSpace.UnityWorldFrame);

            string oldCreated = existing == null ? null : existing.CreatedAtUtc;
            string oldUpdated = existing == null ? null : existing.UpdatedAtUtc;
            string newCreated = incoming == null ? null : incoming.CreatedAtUtc;
            string newUpdated = incoming == null ? null : incoming.UpdatedAtUtc;
            space.CreatedAtUtc = ExtrasTime.Earliest(oldCreated, oldUpdated, newCreated);
            space.UpdatedAtUtc = ExtrasTime.Latest(oldUpdated, newUpdated);

            if (existing != null)
            {
                OverlayUnknown(space.Unknown, existing.Unknown);
            }

            if (incoming != null)
            {
                OverlayUnknown(space.Unknown, incoming.Unknown);
            }

            return space;
        }

        // Adds the items in order; an id seen before replaces the earlier item where it stands.
        private static void Upsert<T>(List<T> into, Dictionary<string, int> positions, List<T> items, Func<T, string> idOf, ref int withoutId)
            where T : class
        {
            foreach (T item in items)
            {
                if (item == null)
                {
                    continue;
                }

                string id = idOf(item);
                if (string.IsNullOrEmpty(id))
                {
                    into.Add(item);
                    withoutId++;
                    continue;
                }

                int position;
                if (positions.TryGetValue(id, out position))
                {
                    into[position] = item;
                }
                else
                {
                    positions[id] = into.Count;
                    into.Add(item);
                }
            }
        }

        private static void WarnAboutMissingIds(List<string> warnings, string noun, ref int count)
        {
            if (count > 0)
            {
                warnings.Add(count + " " + noun + " entr" + (count == 1 ? "y has" : "ies have") + " no id and cannot be matched with an earlier one; each was kept.");
            }

            count = 0;
        }

        private static bool HasAny(List<ScanPlane> planes)
        {
            foreach (ScanPlane plane in planes)
            {
                if (plane != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void OverlayUnknown(JObject target, JObject source)
        {
            foreach (JProperty property in source.Properties())
            {
                target[property.Name] = property.Value.DeepClone();
            }
        }

        // The box around both; a box that is alone is copied, so the merged document owns its bounds.
        private static ScanBounds Union(ScanBounds content, ScanBounds incoming)
        {
            if (content == null && incoming == null)
            {
                return null;
            }

            if (content == null)
            {
                return Copy(incoming);
            }

            if (incoming == null || !incoming.Min.IsFinite || !incoming.Max.IsFinite)
            {
                return content;
            }

            return ScanBounds.FromMinMax(Vec3.Min(content.Min, incoming.Min), Vec3.Max(content.Max, incoming.Max));
        }

        private static ScanBounds Copy(ScanBounds bounds)
        {
            var copy = new ScanBounds(bounds.Center, bounds.Size);
            OverlayUnknown(copy.Unknown, bounds.Unknown);
            return copy;
        }
    }
}
