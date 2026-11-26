using System;
using System.Collections.Generic;
using Companion.Spatial.Vocabulary;
using Newtonsoft.Json.Linq;

namespace Companion.Spatial.Extras
{
    /// <summary>Reads a v2 document (or a newer one, as v2). Lenient: see <see cref="ExtrasReader"/>.</summary>
    internal static class ExtrasV2Reader
    {
        internal static ScanExtras Read(JObject root, ReadContext context)
        {
            var extras = new ScanExtras();

            string units = ExtrasJson.AsString(root["units"]);
            if (units != null && !string.Equals(units, "meters", StringComparison.OrdinalIgnoreCase))
            {
                context.Warn("units is \"" + units + "\", not meters; the numbers were read as meters");
            }

            ReadSpace(root["space"], extras.Space, context);
            extras.Consent = ReadConsent(root["consent"], context);
            extras.Device = ReadDevice(root["device"], context);

            ReadList(root, "frames", context, extras.Frames, ReadFrame, frame => frame.Id, (frame, id) => frame.Id = id);
            ReadList(root, "objects", context, extras.Objects, ReadObject, obj => obj.Id, (obj, id) => obj.Id = id);
            ReadList(root, "planes", context, extras.Planes, ReadPlane, plane => plane.Id, (plane, id) => plane.Id = id);
            extras.Bounds = ReadBounds(root["bounds"], "bounds", context);

            ExtrasJson.KeepUnknown(root, extras, ExtrasKeys.Root);
            return extras;
        }

        // ---- the parts of the document ----

        private static void ReadSpace(JToken token, ScanSpace space, ReadContext context)
        {
            if (!ExtrasJson.IsPresent(token))
            {
                return;
            }

            var obj = token as JObject;
            if (obj == null)
            {
                context.Warn("space is " + ExtrasJson.Describe(token) + ", not an object; it was ignored");
                return;
            }

            string id = ExtrasJson.AsString(obj["id"]);
            if (!string.IsNullOrEmpty(id))
            {
                space.Id = id;
            }

            string frame = ExtrasJson.AsString(obj["frame"]);
            if (!string.IsNullOrEmpty(frame))
            {
                space.Frame = frame;
            }

            space.CreatedAtUtc = ExtrasJson.NullIfEmpty(ExtrasJson.AsString(obj["created_at_utc"]));
            space.UpdatedAtUtc = ExtrasJson.NullIfEmpty(ExtrasJson.AsString(obj["updated_at_utc"]));
            ExtrasJson.KeepUnknown(obj, space, ExtrasKeys.Space);
        }

        private static ScanConsent ReadConsent(JToken token, ReadContext context)
        {
            if (!ExtrasJson.IsPresent(token))
            {
                return null;
            }

            var obj = token as JObject;
            if (obj == null)
            {
                context.Warn("consent is " + ExtrasJson.Describe(token) + ", not an object; it was ignored");
                return null;
            }

            var consent = new ScanConsent
            {
                NoticeVersion = ExtrasJson.NullIfEmpty(ExtrasJson.AsString(obj["notice_version"])),
                AgreedAtUtc = ExtrasJson.NullIfEmpty(ExtrasJson.AsString(obj["agreed_at_utc"])),
                Server = ExtrasJson.NullIfEmpty(ExtrasJson.AsString(obj["server"]))
            };
            ExtrasJson.KeepUnknown(obj, consent, ExtrasKeys.Consent);
            if (consent.NoticeVersion == null)
            {
                context.Warn("consent has no notice_version");
            }

            if (consent.AgreedAtUtc == null)
            {
                context.Warn("consent has no agreed_at_utc");
            }

            return consent;
        }

        private static ScanDevice ReadDevice(JToken token, ReadContext context)
        {
            if (!ExtrasJson.IsPresent(token))
            {
                return null;
            }

            var obj = token as JObject;
            if (obj == null)
            {
                context.Warn("device is " + ExtrasJson.Describe(token) + ", not an object; it was ignored");
                return null;
            }

            var device = new ScanDevice
            {
                Model = ExtrasJson.NullIfEmpty(ExtrasJson.AsString(obj["model"])),
                Os = ExtrasJson.NullIfEmpty(ExtrasJson.AsString(obj["os"])),
                App = ExtrasJson.NullIfEmpty(ExtrasJson.AsString(obj["app"])),
                AppVersion = ExtrasJson.NullIfEmpty(ExtrasJson.AsString(obj["app_version"]))
            };
            ExtrasJson.KeepUnknown(obj, device, ExtrasKeys.Device);
            return device;
        }

        // Reads a list of objects; an item that is not an object, or that the reader gives up on, is skipped.
        private static void ReadList<T>(JObject root, string key, ReadContext context, List<T> into, Func<JObject, string, ReadContext, T> read, Func<T, string> idOf, Action<T, string> setId)
            where T : class
        {
            JToken token = root[key];
            if (!ExtrasJson.IsPresent(token))
            {
                return;
            }

            var array = token as JArray;
            if (array == null)
            {
                context.Warn(key + " is " + ExtrasJson.Describe(token) + ", not a list; it was ignored");
                return;
            }

            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < array.Count; i++)
            {
                string path = ReadContext.At(key, i);
                var item = array[i] as JObject;
                if (item == null)
                {
                    context.Warn(path + " is " + ExtrasJson.Describe(array[i]) + ", not an object; it was skipped");
                    continue;
                }

                T value = read(item, path, context);
                if (value == null)
                {
                    continue;
                }

                string id = idOf(value);
                if (string.IsNullOrEmpty(id))
                {
                    id = SingularPrefix(key) + "-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    context.Warn(path + " has no id; \"" + id + "\" was used");
                    setId(value, id);
                }

                context.AddById(into, positions, value, id, key);
            }
        }

        private static string SingularPrefix(string listName)
        {
            switch (listName)
            {
                case "frames": return "frame";
                case "objects": return "object";
                case "planes": return "plane";
                default: return listName;
            }
        }

        // ---- frames ----

        private static ScanFrame ReadFrame(JObject obj, string path, ReadContext context)
        {
            var frame = new ScanFrame
            {
                Id = ExtrasJson.AsString(obj["id"]) ?? string.Empty,
                TimestampUtc = ExtrasJson.NullIfEmpty(ExtrasJson.AsString(obj["timestamp_utc"])),
                Image = ExtrasJson.NullIfEmpty(ExtrasJson.AsString(obj["image"])),
                Pose = ReadPose(obj["pose"], path + ".pose", context),
                Intrinsics = ReadIntrinsics(obj["intrinsics"], path + ".intrinsics", context)
            };
            ExtrasJson.KeepUnknown(obj, frame, ExtrasKeys.Frame);
            return frame;
        }

        internal static ScanPose ReadPose(JToken token, string path, ReadContext context)
        {
            if (!ExtrasJson.IsPresent(token))
            {
                return null;
            }

            var obj = token as JObject;
            if (obj == null)
            {
                context.Warn(path + " is " + ExtrasJson.Describe(token) + ", not a pose; it was ignored");
                return null;
            }

            Vec3 position;
            if (!ExtrasJson.TryVec3(obj["position"], out position))
            {
                context.Warn(path + " has no valid position; the pose was ignored");
                return null;
            }

            Quat rotation = Quat.Identity;
            JToken quaternion = ExtrasJson.IsPresent(obj["quaternion"]) ? obj["quaternion"] : obj["rotation"];
            if (ExtrasJson.IsPresent(quaternion) && !ExtrasJson.TryQuat(quaternion, out rotation))
            {
                context.Warn(path + " has a rotation that is not four numbers; no rotation was used");
                rotation = Quat.Identity;
            }

            var pose = new ScanPose(position, rotation);
            ExtrasJson.KeepUnknown(obj, pose, ExtrasKeys.Pose);
            return pose;
        }

        private static ScanIntrinsics ReadIntrinsics(JToken token, string path, ReadContext context)
        {
            if (!ExtrasJson.IsPresent(token))
            {
                return null;
            }

            var obj = token as JObject;
            double fx, fy, cx, cy;
            if (obj == null || !ExtrasJson.TryDouble(obj["fx"], out fx) || !ExtrasJson.TryDouble(obj["fy"], out fy) ||
                !ExtrasJson.TryDouble(obj["cx"], out cx) || !ExtrasJson.TryDouble(obj["cy"], out cy))
            {
                context.Warn(path + " needs the numbers fx, fy, cx and cy; it was ignored");
                return null;
            }

            if (!(fx > 0.0) || !(fy > 0.0))
            {
                context.Warn(path + " has a focal length (fx or fy) that is not above 0; it was ignored");
                return null;
            }

            var intrinsics = new ScanIntrinsics { Fx = fx, Fy = fy, Cx = cx, Cy = cy };
            int size;
            if (ExtrasJson.TryInt(obj["width"], out size) && size > 0)
            {
                intrinsics.Width = size;
            }

            if (ExtrasJson.TryInt(obj["height"], out size) && size > 0)
            {
                intrinsics.Height = size;
            }

            ExtrasJson.KeepUnknown(obj, intrinsics, ExtrasKeys.Intrinsics);
            return intrinsics;
        }

        // ---- objects ----

        private static ScanObject ReadObject(JObject obj, string path, ReadContext context)
        {
            var result = new ScanObject
            {
                Id = ExtrasJson.AsString(obj["id"]) ?? string.Empty,
                TrackId = ReadInt(obj, "track_id", path, context),
                Label = ReadLabel(obj["label"], path + ".label", context),
                Confidence = ReadUnitNumber(obj, "confidence", path, context),
                Source = ReadSource(obj["source"], path, context),
                AnchorUuid = ExtrasJson.NullIfEmpty(ExtrasJson.AsString(obj["anchor_uuid"])),
                Bounds = ReadBounds(obj["bounds"], path + ".bounds", context)
            };

            ScanPose pose = ReadPose(obj["pose"], path + ".pose", context);
            if (pose != null)
            {
                result.Pose = pose;
            }

            JToken normal = obj["normal"];
            Vec3 normalVector;
            if (ExtrasJson.IsPresent(normal))
            {
                if (ExtrasJson.TryVec3(normal, out normalVector))
                {
                    result.Normal = normalVector;
                }
                else
                {
                    context.Warn(path + ".normal is not three numbers; it was ignored");
                }
            }

            ReadObservations(obj["observations"], result.Observations, path + ".observations", context);
            ExtrasJson.KeepUnknown(obj, result, ExtrasKeys.Obj);
            return result;
        }

        private static ScanLabel ReadLabel(JToken token, string path, ReadContext context)
        {
            TagVocabulary vocabulary = context.Vocabulary;
            var label = new ScanLabel();
            if (!ExtrasJson.IsPresent(token))
            {
                return label;
            }

            if (token.Type == JTokenType.String)
            {
                label.Text = (string)token;
                label.Tag = vocabulary.Normalize(label.Text);
                label.Semantic = vocabulary.ToSemanticLabel(label.Text);
                return label;
            }

            var obj = token as JObject;
            if (obj == null)
            {
                context.Warn(path + " is " + ExtrasJson.Describe(token) + ", not text or an object; it was ignored");
                return label;
            }

            // What is written is kept as it is, even when empty; only what is missing is filled in.
            string text = ExtrasJson.AsString(obj["text"]);
            string tag = ExtrasJson.AsString(obj["tag"]);
            if (text == null)
            {
                text = tag ?? string.Empty;
            }

            if (tag == null)
            {
                tag = vocabulary.Normalize(text);
            }

            label.Text = text;
            label.Tag = tag;
            label.Semantic = ReadSemantic(obj["semantic"], tag.Length > 0 ? tag : text, path + ".semantic", context);
            ExtrasJson.KeepUnknown(obj, label, ExtrasKeys.Label);
            return label;
        }

        // The Meta label if it is one; otherwise the vocabulary's answer for the tag, with a warning when a value was there.
        internal static string ReadSemantic(JToken token, string tagOrText, string path, ReadContext context)
        {
            string given = ExtrasJson.AsString(token);
            if (given != null && MetaLabels.IsMetaLabel(given))
            {
                return given;
            }

            if (ExtrasJson.IsPresent(token))
            {
                context.Warn(path + " is not one of the 16 Meta labels; it was derived from the tag");
            }

            return context.Vocabulary.ToSemanticLabel(tagOrText);
        }

        private static ObjectSource ReadSource(JToken token, string path, ReadContext context)
        {
            if (!ExtrasJson.IsPresent(token))
            {
                return ObjectSource.Cv;
            }

            string text = ExtrasJson.AsString(token);
            ObjectSource source;
            if (text != null && TryParseSource(text, out source))
            {
                return source;
            }

            context.Warn(path + ".source is not cv, device or manual; cv was used");
            return ObjectSource.Cv;
        }

        internal static bool TryParseSource(string text, out ObjectSource source)
        {
            switch (text.Trim().ToLowerInvariant())
            {
                case "cv":
                case "cvserver":
                    source = ObjectSource.Cv;
                    return true;
                case "device":
                case "metascene":
                    source = ObjectSource.Device;
                    return true;
                case "manual":
                    source = ObjectSource.Manual;
                    return true;
                default:
                    source = ObjectSource.Cv;
                    return false;
            }
        }

        private static void ReadObservations(JToken token, List<ScanObservation> into, string path, ReadContext context)
        {
            if (!ExtrasJson.IsPresent(token))
            {
                return;
            }

            var array = token as JArray;
            if (array == null)
            {
                context.Warn(path + " is " + ExtrasJson.Describe(token) + ", not a list; it was ignored");
                return;
            }

            for (int i = 0; i < array.Count; i++)
            {
                string at = ReadContext.At(path, i);
                var obj = array[i] as JObject;
                if (obj == null)
                {
                    context.Warn(at + " is " + ExtrasJson.Describe(array[i]) + ", not an object; it was skipped");
                    continue;
                }

                var observation = new ScanObservation
                {
                    FrameId = ExtrasJson.AsString(obj["frame_id"]) ?? string.Empty,
                    Score = ReadUnitNumber(obj, "score", at, context)
                };

                JToken boxToken = obj["bbox_cxcywh"];
                if (ExtrasJson.IsPresent(boxToken))
                {
                    NormalizedBox box;
                    if (TryReadBox(boxToken, out box))
                    {
                        observation.Box = box;
                    }
                    else
                    {
                        context.Warn(at + ".bbox_cxcywh is not [cx, cy, w, h] of a box inside the image; the box was left out");
                    }
                }

                ExtrasJson.KeepUnknown(obj, observation, ExtrasKeys.Observation);
                into.Add(observation);
            }
        }

        private static bool TryReadBox(JToken token, out NormalizedBox box)
        {
            box = default;
            var array = token as JArray;
            double cx, cy, w, h;
            if (array == null || array.Count != 4 || !ExtrasJson.TryDouble(array[0], out cx) || !ExtrasJson.TryDouble(array[1], out cy) ||
                !ExtrasJson.TryDouble(array[2], out w) || !ExtrasJson.TryDouble(array[3], out h))
            {
                return false;
            }

            return NormalizedBox.TryFromCxCyWh(cx, cy, w, h, out box);
        }

        // ---- planes ----

        private static ScanPlane ReadPlane(JObject obj, string path, ReadContext context)
        {
            var plane = new ScanPlane
            {
                Id = ExtrasJson.AsString(obj["id"]) ?? string.Empty,
                Label = ExtrasJson.AsString(obj["label"]) ?? string.Empty
            };
            plane.Semantic = ReadSemantic(obj["semantic"], plane.Label, path + ".semantic", context);

            ScanPose pose = ReadPose(obj["pose"], path + ".pose", context);
            if (pose != null)
            {
                plane.Pose = pose;
            }

            plane.Center = ReadVec2(obj, "center", path, context);
            plane.Size = ReadVec2(obj, "size", path, context);

            JToken boundary = obj["boundary"];
            if (ExtrasJson.IsPresent(boundary))
            {
                var points = boundary as JArray;
                if (points == null)
                {
                    context.Warn(path + ".boundary is " + ExtrasJson.Describe(boundary) + ", not a list; it was ignored");
                }
                else
                {
                    for (int i = 0; i < points.Count; i++)
                    {
                        Vec3 point;
                        if (ExtrasJson.TryVec3(points[i], out point))
                        {
                            plane.Boundary.Add(point);
                        }
                        else
                        {
                            context.Warn(ReadContext.At(path + ".boundary", i) + " is not three numbers; the point was skipped");
                        }
                    }
                }
            }

            ExtrasJson.KeepUnknown(obj, plane, ExtrasKeys.Plane);
            return plane;
        }

        private static Vec2? ReadVec2(JObject obj, string key, string path, ReadContext context)
        {
            JToken token = obj[key];
            if (!ExtrasJson.IsPresent(token))
            {
                return null;
            }

            Vec2 value;
            if (ExtrasJson.TryVec2(token, out value))
            {
                return value;
            }

            context.Warn(path + "." + key + " is not two numbers; it was ignored");
            return null;
        }

        // ---- bounds and numbers ----

        internal static ScanBounds ReadBounds(JToken token, string path, ReadContext context)
        {
            if (!ExtrasJson.IsPresent(token))
            {
                return null;
            }

            var obj = token as JObject;
            Vec3 center, size;
            if (obj == null || !ExtrasJson.TryVec3(obj["center"], out center) || !ExtrasJson.TryVec3(obj["size"], out size))
            {
                context.Warn(path + " needs a center and a size, each three numbers; it was ignored");
                return null;
            }

            var bounds = new ScanBounds(center, size);
            ExtrasJson.KeepUnknown(obj, bounds, ExtrasKeys.Bounds);
            return bounds;
        }

        // A number from 0 to 1 (clamped); 0 when missing, and 0 with a warning when it is there but not a number.
        private static double ReadUnitNumber(JObject obj, string key, string path, ReadContext context)
        {
            JToken token = obj[key];
            if (token == null)
            {
                return 0.0;
            }

            double value;
            if (!ExtrasJson.TryDouble(token, out value))
            {
                bool isNumber = token.Type == JTokenType.Integer || token.Type == JTokenType.Float;
                context.Warn(path + "." + key + " is " + (isNumber ? "not a finite number" : ExtrasJson.Describe(token) + ", not a number") + "; 0 was used");
                return 0.0;
            }

            return ExtrasJson.Clamp01(value);
        }

        private static int ReadInt(JObject obj, string key, string path, ReadContext context)
        {
            JToken token = obj[key];
            if (token == null)
            {
                return 0;
            }

            int value;
            if (!ExtrasJson.TryInt(token, out value))
            {
                context.Warn(path + "." + key + " is not a whole number; 0 was used");
                return 0;
            }

            if (value < 0)
            {
                context.Warn(path + "." + key + " is negative; 0 was used");
                return 0;
            }

            return value;
        }
    }
}
