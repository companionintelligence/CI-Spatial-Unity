using System;
using System.Collections.Generic;
using Companion.Spatial.Vocabulary;
using Newtonsoft.Json.Linq;

namespace Companion.Spatial.Extras
{
    /// <summary>
    /// Reads a v1 document, the extras the scanner's <c>SpatialMapExporter</c> wrote until now, into the v2 model. A pin
    /// becomes an object, a snapshot a frame, a Space Setup anchor a plane. The v1 members that v2 has no place for are
    /// kept, as they were, under the key <c>"v1"</c> of the part they belonged to (see <see cref="ExtrasNode.Unknown"/>);
    /// top-level keys the reader does not know at all are kept as they are, except one that has the name of a part of
    /// the v2 document, which goes under <c>"v1"</c> too. See Documentation~/CONTRACT.md for every mapping.
    /// </summary>
    internal static class ExtrasV1Reader
    {
        internal static ScanExtras Read(JObject input, ReadContext context)
        {
            // Work on a copy and take each key out as it is read: what is left is what v2 has no place for.
            JObject root = (JObject)input.DeepClone();
            var extras = new ScanExtras();
            var kept = new JObject();

            string units = TakeString(root, "units");
            if (units != null && !string.Equals(units, "meters", StringComparison.OrdinalIgnoreCase))
            {
                context.Warn("units is \"" + units + "\", not meters; the numbers were read as meters");
            }

            root.Remove("schema_version");
            extras.Space.UpdatedAtUtc = ExtrasJson.NullIfEmpty(TakeString(root, "timestamp_utc"));

            JObject cv = TakeObject(root, "cv_mapping", context);
            string spaceId = ExtrasJson.NullIfEmpty(TakeString(root, "space_id"));
            string cvSpaceId = cv == null ? null : ExtrasJson.NullIfEmpty(TakeString(cv, "space_id"));
            extras.Space.Id = spaceId ?? cvSpaceId ?? ScanSpace.DefaultId;

            // The exporter repeats five members of cv_mapping at the top level: read cv_mapping's, drop the copy.
            JToken pins = Take(cv, root, "pins");
            JToken snapshots = Take(cv, root, "snapshots");
            JToken bounds = Take(cv, root, "bounds");
            JToken boundary = Take(cv, root, "boundary");
            JToken floorplan = Take(cv, root, "floorplan");
            JObject meta = TakeObject(root, "meta_scene", context);

            ReadPins(pins, extras, kept, context);
            ReadSnapshots(snapshots, extras, kept, context);
            ReadAnchors(meta, extras, kept, context);

            Vec3 center, size;
            var boundsObject = bounds as JObject;
            if (boundsObject != null && ExtrasJson.TryVec3(boundsObject["center"], out center) && ExtrasJson.TryVec3(boundsObject["size"], out size))
            {
                extras.Bounds = new ScanBounds(center, size);
            }
            else if (ExtrasJson.IsPresent(bounds))
            {
                context.Warn("bounds is not a center and a size of three numbers each; it was ignored");
            }

            Keep(kept, "meta_scene", meta);
            Keep(kept, "boundary", boundary);
            Keep(kept, "floorplan", floorplan);
            Keep(kept, "cv_mapping", cv);

            // Anything else at the top level is a key the reader does not know. One that has the name of a part of the v2
            // document (a "device" or "objects" some other tool added) would be read as that part by a v2 reader, so it is
            // kept under v1 instead.
            foreach (JProperty property in root.Properties())
            {
                if (Array.IndexOf(ExtrasKeys.Root, property.Name) >= 0)
                {
                    kept[property.Name] = property.Value.DeepClone();
                }
                else
                {
                    extras.Unknown[property.Name] = property.Value.DeepClone();
                }
            }

            if (kept.HasValues)
            {
                extras.Unknown["v1"] = kept;
            }

            return extras;
        }

        // ---- pins -> objects ----

        private static void ReadPins(JToken token, ScanExtras extras, JObject kept, ReadContext context)
        {
            if (!ExtrasJson.IsPresent(token))
            {
                return;
            }

            var array = token as JArray;
            if (array == null)
            {
                context.Warn("pins is " + ExtrasJson.Describe(token) + ", not a list; it was ignored");
                return;
            }

            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < array.Count; i++)
            {
                var pin = array[i] as JObject;
                if (pin == null)
                {
                    context.Warn(ReadContext.At("pins", i) + " is " + ExtrasJson.Describe(array[i]) + ", not an object; it was skipped");
                    continue;
                }

                ScanObject obj = ConvertPin(pin, i, context);
                if (obj == null)
                {
                    KeepSkipped(kept, "skipped_pins", pin);
                    continue;
                }

                context.AddById(extras.Objects, positions, obj, obj.Id, "pins");
            }
        }

        private static ScanObject ConvertPin(JObject source, int index, ReadContext context)
        {
            JObject pin = (JObject)source.DeepClone();
            TagVocabulary vocabulary = context.Vocabulary;
            string at = ReadContext.At("pins", index);

            Vec3 position;
            if (!ExtrasJson.TryVec3(Remove(pin, "position"), out position))
            {
                context.Warn(at + " has no valid position, so it was skipped (it is kept under v1.skipped_pins)");
                return null;
            }

            string label = TakeString(pin, "label") ?? string.Empty;
            string v1Semantic = TakeString(pin, "semantic_label");
            string semantic = v1Semantic != null && v1Semantic != MetaLabels.Other && MetaLabels.IsMetaLabel(v1Semantic)
                ? v1Semantic
                : vocabulary.ToSemanticLabel(label);
            string tag = vocabulary.Normalize(label);
            if (tag.Length == 0 && semantic != MetaLabels.Other)
            {
                tag = vocabulary.Normalize(semantic);
            }

            double score = ExtrasJson.Clamp01(TakeNumber(pin, "score"));
            Quat rotation;
            if (!ExtrasJson.TryQuat(Remove(pin, "rotation"), out rotation))
            {
                rotation = Quat.Identity;
            }

            int trackId = TakeInt(pin, "track_id");
            if (trackId < 0)
            {
                context.Warn(at + ".track_id is negative; 0 (not tracked) was used");
                trackId = 0;
            }

            var obj = new ScanObject
            {
                TrackId = trackId,
                Label = new ScanLabel(label, tag, semantic),
                Confidence = score,
                Source = TakeSource(pin, at, context),
                Pose = new ScanPose(position, rotation),
                AnchorUuid = ExtrasJson.NullIfEmpty(TakeString(pin, "anchor_uuid"))
            };

            Vec3 normal;
            if (ExtrasJson.TryVec3(Remove(pin, "normal"), out normal))
            {
                obj.Normal = normal;
            }

            Vec3 volume;
            if (ExtrasJson.TryVec3(Remove(pin, "volume_size"), out volume) && (volume.X > 0.0 || volume.Y > 0.0 || volume.Z > 0.0))
            {
                obj.Bounds = new ScanBounds(position, volume);
            }

            // The capture the pin came from (the id is kept in the pin, so it is read, not taken, until it is used).
            string captureId = pin["capture_id"] == null ? null : ExtrasJson.NullIfEmpty(ExtrasJson.AsString(pin["capture_id"]));
            if (captureId != null)
            {
                pin.Remove("capture_id");
                var observation = new ScanObservation { FrameId = captureId, Score = score };
                NormalizedBox box;
                if (TryReadXywh(pin["bbox_normalized"], out box))
                {
                    observation.Box = box;
                    pin.Remove("bbox_normalized");
                }

                obj.Observations.Add(observation);
            }

            string session = ExtrasJson.AsString(pin["capture_session_id"]);
            if (obj.AnchorUuid != null)
            {
                obj.Id = ScanObject.MakeAnchorId(obj.AnchorUuid);
            }
            else if (obj.TrackId > 0)
            {
                obj.Id = ScanObject.MakeId(session, obj.TrackId);
            }
            else
            {
                obj.Id = "pin-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            Leftovers(pin, obj);
            return obj;
        }

        private static ObjectSource TakeSource(JObject pin, string at, ReadContext context)
        {
            string text = TakeString(pin, "source");
            if (string.IsNullOrEmpty(text))
            {
                return ObjectSource.Cv;
            }

            ObjectSource source;
            if (ExtrasV2Reader.TryParseSource(text, out source))
            {
                return source;
            }

            context.Warn(at + ".source \"" + text + "\" is not CvServer or MetaScene; cv was used");
            return ObjectSource.Cv;
        }

        // bbox_normalized is [x, y, width, height] from the top left, fractions of the image.
        private static bool TryReadXywh(JToken token, out NormalizedBox box)
        {
            box = default;
            var array = token as JArray;
            double x, y, w, h;
            if (array == null || array.Count != 4 || !ExtrasJson.TryDouble(array[0], out x) || !ExtrasJson.TryDouble(array[1], out y) ||
                !ExtrasJson.TryDouble(array[2], out w) || !ExtrasJson.TryDouble(array[3], out h))
            {
                return false;
            }

            return NormalizedBox.TryFromXywh(x, y, w, h, out box);
        }

        // ---- snapshots -> frames ----

        private static void ReadSnapshots(JToken token, ScanExtras extras, JObject kept, ReadContext context)
        {
            if (!ExtrasJson.IsPresent(token))
            {
                return;
            }

            var array = token as JArray;
            if (array == null)
            {
                context.Warn("snapshots is " + ExtrasJson.Describe(token) + ", not a list; it was ignored");
                return;
            }

            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < array.Count; i++)
            {
                var snapshot = array[i] as JObject;
                if (snapshot == null)
                {
                    context.Warn(ReadContext.At("snapshots", i) + " is " + ExtrasJson.Describe(array[i]) + ", not an object; it was skipped");
                    continue;
                }

                ScanFrame frame = ConvertSnapshot(snapshot, i, context);
                context.AddById(extras.Frames, positions, frame, frame.Id, "snapshots");
            }
        }

        private static ScanFrame ConvertSnapshot(JObject source, int index, ReadContext context)
        {
            JObject snapshot = (JObject)source.DeepClone();
            var frame = new ScanFrame { Id = TakeString(snapshot, "id") ?? string.Empty };
            if (frame.Id.Length == 0)
            {
                frame.Id = "frame-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                context.Warn(ReadContext.At("snapshots", index) + " has no id; \"" + frame.Id + "\" was used");
            }

            frame.Image = ExtrasJson.NullIfEmpty(TakeString(snapshot, "image"));

            JObject camera = Remove(snapshot, "camera") as JObject;
            Vec3 cameraPosition;
            if (camera != null && ExtrasJson.TryVec3(camera["position"], out cameraPosition))
            {
                Quat cameraRotation;
                if (!ExtrasJson.TryQuat(camera["rotation"], out cameraRotation))
                {
                    cameraRotation = Quat.Identity;
                }

                frame.Pose = new ScanPose(cameraPosition, cameraRotation);
            }

            int width = 0;
            int height = 0;
            var size = snapshot["image_size"] as JArray;
            if (size != null && size.Count == 2 && ExtrasJson.TryInt(size[0], out width) && ExtrasJson.TryInt(size[1], out height))
            {
                snapshot.Remove("image_size");
            }
            else
            {
                width = 0;
                height = 0;
            }

            // The 4x4 matrix in row-major order: fx = m00, fy = m11, cx = m02, cy = m12.
            var matrix = snapshot["intrinsics"] as JArray;
            double fx, fy, cx, cy;
            if (matrix != null && matrix.Count == 16 && ExtrasJson.TryDouble(matrix[0], out fx) && ExtrasJson.TryDouble(matrix[5], out fy) &&
                ExtrasJson.TryDouble(matrix[2], out cx) && ExtrasJson.TryDouble(matrix[6], out cy) && fx > 0.0 && fy > 0.0)
            {
                frame.Intrinsics = new ScanIntrinsics { Fx = fx, Fy = fy, Cx = cx, Cy = cy, Width = Math.Max(0, width), Height = Math.Max(0, height) };
                snapshot.Remove("intrinsics");
            }

            Leftovers(snapshot, frame);
            return frame;
        }

        // ---- scene anchors -> planes ----

        private static void ReadAnchors(JObject meta, ScanExtras extras, JObject kept, ReadContext context)
        {
            if (meta == null)
            {
                return;
            }

            JToken token = Remove(meta, "anchors");
            if (!ExtrasJson.IsPresent(token))
            {
                return;
            }

            var array = token as JArray;
            if (array == null)
            {
                context.Warn("meta_scene.anchors is " + ExtrasJson.Describe(token) + ", not a list; it was ignored");
                return;
            }

            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < array.Count; i++)
            {
                var anchor = array[i] as JObject;
                if (anchor == null)
                {
                    context.Warn(ReadContext.At("meta_scene.anchors", i) + " is " + ExtrasJson.Describe(array[i]) + ", not an object; it was skipped");
                    continue;
                }

                ScanPlane plane = ConvertAnchor(anchor, i, context);
                if (plane == null)
                {
                    KeepSkipped(kept, "skipped_anchors", anchor);
                    continue;
                }

                context.AddById(extras.Planes, positions, plane, plane.Id, "meta_scene.anchors");
            }
        }

        private static ScanPlane ConvertAnchor(JObject source, int index, ReadContext context)
        {
            JObject anchor = (JObject)source.DeepClone();
            TagVocabulary vocabulary = context.Vocabulary;
            string at = ReadContext.At("meta_scene.anchors", index);

            Vec3 position;
            if (!ExtrasJson.TryVec3(Remove(anchor, "position"), out position))
            {
                context.Warn(at + " has no valid position, so it was skipped (it is kept under v1.skipped_anchors)");
                return null;
            }

            var plane = new ScanPlane { Id = TakeString(anchor, "uuid") ?? string.Empty };
            if (plane.Id.Length == 0)
            {
                plane.Id = "plane-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                context.Warn(at + " has no id; \"" + plane.Id + "\" was used");
            }

            Quat rotation;
            if (!ExtrasJson.TryQuat(Remove(anchor, "rotation"), out rotation))
            {
                rotation = Quat.Identity;
            }

            plane.Pose = new ScanPose(position, rotation);

            // The first label names the plane; an anchor with several keeps the whole list under v1.
            string primary = null;
            var labels = anchor["labels"] as JArray;
            if (labels != null)
            {
                foreach (JToken label in labels)
                {
                    string text = ExtrasJson.AsString(label);
                    if (!string.IsNullOrEmpty(text))
                    {
                        primary = text;
                        break;
                    }
                }

                if (labels.Count <= 1)
                {
                    anchor.Remove("labels");
                }
            }

            string tag = vocabulary.Normalize(primary);
            plane.Label = tag.Length > 0 ? tag : "other";
            plane.Semantic = vocabulary.ToSemanticLabel(primary);

            // The plane's rectangle is only meant when has_plane says so.
            JToken hasPlaneToken = Remove(anchor, "has_plane");
            bool hasPlane = hasPlaneToken != null && hasPlaneToken.Type == JTokenType.Boolean ? (bool)hasPlaneToken : anchor["plane_size"] != null;
            JToken center = Remove(anchor, "plane_center");
            JToken size = Remove(anchor, "plane_size");
            JToken boundary = Remove(anchor, "plane_boundary");
            if (hasPlane)
            {
                Vec2 vector;
                if (ExtrasJson.TryVec2(center, out vector))
                {
                    plane.Center = vector;
                }

                if (ExtrasJson.TryVec2(size, out vector))
                {
                    plane.Size = vector;
                }

                var points = boundary as JArray;
                if (points != null)
                {
                    foreach (JToken point in points)
                    {
                        Vec3 vertex;
                        if (ExtrasJson.TryVec3(point, out vertex))
                        {
                            plane.Boundary.Add(vertex);
                        }
                    }
                }
            }

            Leftovers(anchor, plane);
            return plane;
        }

        // ---- helpers ----

        // Takes the cv_mapping copy of a member if it has one, else the top-level one; both are removed.
        private static JToken Take(JObject primary, JObject secondary, string key)
        {
            JToken value = primary == null ? null : Remove(primary, key);
            JToken copy = Remove(secondary, key);
            return ExtrasJson.IsPresent(value) ? value : copy;
        }

        private static JToken Remove(JObject obj, string key)
        {
            JToken value = obj[key];
            obj.Remove(key);
            return value;
        }

        private static string TakeString(JObject obj, string key)
        {
            return ExtrasJson.AsString(Remove(obj, key));
        }

        private static double TakeNumber(JObject obj, string key)
        {
            double value;
            return ExtrasJson.TryDouble(Remove(obj, key), out value) ? value : 0.0;
        }

        private static int TakeInt(JObject obj, string key)
        {
            int value;
            return ExtrasJson.TryInt(Remove(obj, key), out value) ? value : 0;
        }

        private static JObject TakeObject(JObject obj, string key, ReadContext context)
        {
            JToken token = Remove(obj, key);
            if (!ExtrasJson.IsPresent(token))
            {
                return null;
            }

            var result = token as JObject;
            if (result == null)
            {
                context.Warn(key + " is " + ExtrasJson.Describe(token) + ", not an object; it was ignored");
            }

            return result;
        }

        // What the reader did not take is the v1 leftovers of the node: it goes under "v1". A null, an empty string,
        // an empty list and an empty object say nothing and are not kept.
        private static void Leftovers(JObject remaining, ExtrasNode node)
        {
            var kept = new JObject();
            foreach (JProperty property in remaining.Properties())
            {
                if (HasContent(property.Value))
                {
                    kept[property.Name] = property.Value.DeepClone();
                }
            }

            if (kept.HasValues)
            {
                node.Unknown["v1"] = kept;
            }
        }

        private static void Keep(JObject kept, string key, JToken value)
        {
            if (value == null)
            {
                return;
            }

            var obj = value as JObject;
            if (obj != null)
            {
                var content = new JObject();
                foreach (JProperty property in obj.Properties())
                {
                    if (HasContent(property.Value))
                    {
                        content[property.Name] = property.Value.DeepClone();
                    }
                }

                if (content.HasValues)
                {
                    kept[key] = content;
                }

                return;
            }

            if (HasContent(value))
            {
                kept[key] = value.DeepClone();
            }
        }

        private static void KeepSkipped(JObject kept, string key, JObject entry)
        {
            var list = kept[key] as JArray;
            if (list == null)
            {
                list = new JArray();
                kept[key] = list;
            }

            list.Add(entry.DeepClone());
        }

        private static bool HasContent(JToken value)
        {
            switch (value.Type)
            {
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return false;
                case JTokenType.String:
                    return ((string)value).Length > 0;
                case JTokenType.Array:
                case JTokenType.Object:
                    return value.HasValues;
                default:
                    return true;
            }
        }
    }
}
