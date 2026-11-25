using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace CompanionScan.Export
{
    /// <summary>
    /// Merges the extras of a new export (schema 1.0, as the exporter writes them; the Companion.Spatial package has the v2 merge) of a space into the extras of the earlier export of the same
    /// space (the glTF is rewritten on every save, and what an earlier save found is kept). A pin or a
    /// snapshot that the earlier export also had replaces its entry instead of being listed twice, so
    /// saving again and again does not grow the file.
    /// </summary>
    public static class ExtrasMergeV1
    {
        /// <summary>
        /// The merge. Without earlier extras it is <paramref name="current"/> itself, and without new ones
        /// <paramref name="existing"/> itself. Otherwise everything the earlier file had stays (including
        /// members this code does not know), except what the new export states afresh: the time, the
        /// bounds, the boundary, the floor plan, the scene and the space id.
        /// </summary>
        public static JObject Merge(JObject existing, JObject current)
        {
            if (existing == null)
            {
                return current;
            }

            if (current == null)
            {
                return existing;
            }

            var merged = new JObject(existing);

            // Everything the new export states is the newest word, except the two lists, merged below.
            foreach (var property in current.Properties())
            {
                if (property.Name == "pins" || property.Name == "snapshots" || property.Name == "cv_mapping")
                {
                    continue;
                }

                merged[property.Name] = property.Value.DeepClone();
            }

            merged["pins"] = MergeList(existing["pins"] as JArray, current["pins"] as JArray, PinKey) ?? new JArray();
            SetOrRemove(merged, "snapshots", MergeList(existing["snapshots"] as JArray, current["snapshots"] as JArray, SnapshotKey));

            var existingCv = existing["cv_mapping"] as JObject;
            var currentCv = current["cv_mapping"] as JObject;
            var mergedCv = existingCv != null ? new JObject(existingCv) : new JObject();
            if (currentCv != null)
            {
                foreach (var property in currentCv.Properties())
                {
                    if (property.Name != "pins" && property.Name != "snapshots")
                    {
                        mergedCv[property.Name] = property.Value.DeepClone();
                    }
                }
            }

            mergedCv["pins"] = MergeList(existingCv?["pins"] as JArray, currentCv?["pins"] as JArray, PinKey) ?? new JArray();
            SetOrRemove(mergedCv, "snapshots", MergeList(existingCv?["snapshots"] as JArray, currentCv?["snapshots"] as JArray, SnapshotKey));
            merged["cv_mapping"] = mergedCv;

            return merged;
        }

        /// <summary>
        /// What makes a pin the same object in two exports: a Space Setup anchor is the same in every
        /// session; a vision server pin is the same track of the same capture session (track numbers
        /// start again at 1 in every session); a pin with neither is the same label, snapshot and place.
        /// </summary>
        public static string PinKey(JToken pin)
        {
            var anchor = (string)pin["anchor_uuid"];
            if (!string.IsNullOrEmpty(anchor))
            {
                return "anchor:" + anchor;
            }

            int track = pin["track_id"] != null && pin["track_id"].Type != JTokenType.Null ? (int)pin["track_id"] : 0;
            var session = (string)pin["capture_session_id"];
            if (track > 0 && !string.IsNullOrEmpty(session))
            {
                return "cv:" + session + ":" + track.ToString(CultureInfo.InvariantCulture);
            }

            return "pin:" + (string)pin["label"] + "|" + (string)pin["capture_id"] + "|" + PositionKey(pin["position"]);
        }

        /// <summary>A snapshot is the same snapshot by its id; one without an id is never taken for another.</summary>
        public static string SnapshotKey(JToken snapshot)
        {
            var id = (string)snapshot["id"];
            return string.IsNullOrEmpty(id) ? null : "snapshot:" + id;
        }

        private static string PositionKey(JToken position)
        {
            var array = position as JArray;
            if (array == null || array.Count < 3)
            {
                return string.Empty;
            }

            return string.Join(",",
                ((float)array[0]).ToString("R", CultureInfo.InvariantCulture),
                ((float)array[1]).ToString("R", CultureInfo.InvariantCulture),
                ((float)array[2]).ToString("R", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// The earlier entries then the new ones, each key once: a later entry with the same key takes the
        /// place of the earlier one, so the order is the order of first appearance. An entry without a key
        /// is always kept. Null when there is nothing on either side.
        /// </summary>
        private static JArray MergeList(JArray existing, JArray current, System.Func<JToken, string> keyOf)
        {
            if ((existing == null || existing.Count == 0) && (current == null || current.Count == 0))
            {
                return null;
            }

            var merged = new JArray();
            var positions = new Dictionary<string, int>();
            foreach (var list in new[] { existing, current })
            {
                if (list == null)
                {
                    continue;
                }

                foreach (var item in list)
                {
                    var key = item is JObject ? keyOf(item) : null;
                    if (key != null && positions.TryGetValue(key, out var at))
                    {
                        merged[at] = item.DeepClone();
                        continue;
                    }

                    if (key != null)
                    {
                        positions[key] = merged.Count;
                    }

                    merged.Add(item.DeepClone());
                }
            }

            return merged;
        }

        private static void SetOrRemove(JObject target, string name, JArray value)
        {
            if (value == null)
            {
                target.Remove(name);
            }
            else
            {
                target[name] = value;
            }
        }
    }
}
