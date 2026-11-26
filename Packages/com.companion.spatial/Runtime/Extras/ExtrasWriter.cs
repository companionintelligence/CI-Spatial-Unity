using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Companion.Spatial.Vocabulary;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Companion.Spatial.Extras
{
    /// <summary>
    /// Writes the extras model as the v2 JSON document: the object that goes into the root <c>extras</c> of the glTF.
    /// The key order is fixed (the keys of the schema in the order they are listed there, then the unknown keys in the
    /// order they were read), so the same model always gives the same text. Every list is written as an array, an
    /// empty one when there is nothing in it (v1 wrote <c>"pins": null</c>). A part that is not there is left out,
    /// not written as null. The writer also repairs what JSON and the schema cannot hold: NaN and infinity become 0, a
    /// confidence or score is held to 0..1, a negative track id becomes 0, a Meta label that is not one of the 16 becomes
    /// OTHER, and an unknown key is never written under the name of a part of the document. What it cannot repair is
    /// written as it is, and the schema flags it: a time that is not a time, a focal length of 0, a consent without its time.
    /// </summary>
    public static class ExtrasWriter
    {
        /// <summary>Writes the model as a JSON object. The model is not changed and the result shares nothing with it.</summary>
        /// <param name="extras">The model.</param>
        /// <exception cref="ArgumentNullException"><paramref name="extras"/> is null.</exception>
        public static JObject Write(ScanExtras extras)
        {
            if (extras == null)
            {
                throw new ArgumentNullException(nameof(extras));
            }

            var root = new JObject
            {
                ["schema"] = ScanExtras.SchemaId,
                ["schema_version"] = ScanExtras.CurrentVersion,
                ["units"] = "meters",
                ["space"] = WriteSpace(extras.Space)
            };

            if (extras.Consent != null)
            {
                root["consent"] = WriteConsent(extras.Consent);
            }

            if (extras.Device != null)
            {
                root["device"] = WriteDevice(extras.Device);
            }

            root["frames"] = WriteList(extras.Frames, WriteFrame);
            root["objects"] = WriteList(extras.Objects, WriteObject);
            root["planes"] = WriteList(extras.Planes, WritePlane);
            if (extras.Bounds != null)
            {
                root["bounds"] = WriteBounds(extras.Bounds);
            }

            ExtrasJson.AppendUnknown(root, extras.Unknown, ExtrasKeys.Root);
            return root;
        }

        /// <summary>
        /// Writes the model as JSON text, with line feeds only (so the text is the same on every machine) and no byte
        /// order mark.
        /// </summary>
        /// <param name="extras">The model.</param>
        /// <param name="indented">True (the default) for two-space indentation, false for one line.</param>
        /// <exception cref="ArgumentNullException"><paramref name="extras"/> is null.</exception>
        public static string ToJson(ScanExtras extras, bool indented = true)
        {
            JObject json = Write(extras);
            var builder = new StringBuilder();
            using (var text = new StringWriter(builder, CultureInfo.InvariantCulture) { NewLine = "\n" })
            using (var writer = new JsonTextWriter(text) { Formatting = indented ? Formatting.Indented : Formatting.None, Indentation = 2, IndentChar = ' ' })
            {
                json.WriteTo(writer);
            }

            return builder.ToString();
        }

        private static JArray WriteList<T>(List<T> items, Func<T, JObject> write)
            where T : class
        {
            var array = new JArray();
            foreach (T item in items)
            {
                if (item != null)
                {
                    array.Add(write(item));
                }
            }

            return array;
        }

        private static JObject WriteSpace(ScanSpace space)
        {
            space = space ?? new ScanSpace();
            var obj = new JObject
            {
                ["id"] = string.IsNullOrEmpty(space.Id) ? ScanSpace.DefaultId : space.Id,
                ["frame"] = string.IsNullOrEmpty(space.Frame) ? ScanSpace.UnityWorldFrame : space.Frame
            };
            ExtrasJson.SetIfNotEmpty(obj, "created_at_utc", space.CreatedAtUtc);
            ExtrasJson.SetIfNotEmpty(obj, "updated_at_utc", space.UpdatedAtUtc);
            ExtrasJson.AppendUnknown(obj, space.Unknown, ExtrasKeys.Space);
            return obj;
        }

        private static JObject WriteConsent(ScanConsent consent)
        {
            var obj = new JObject();
            ExtrasJson.SetIfNotEmpty(obj, "notice_version", consent.NoticeVersion);
            ExtrasJson.SetIfNotEmpty(obj, "agreed_at_utc", consent.AgreedAtUtc);
            ExtrasJson.SetIfNotEmpty(obj, "server", consent.Server);
            ExtrasJson.AppendUnknown(obj, consent.Unknown, ExtrasKeys.Consent);
            return obj;
        }

        private static JObject WriteDevice(ScanDevice device)
        {
            var obj = new JObject();
            ExtrasJson.SetIfNotEmpty(obj, "model", device.Model);
            ExtrasJson.SetIfNotEmpty(obj, "os", device.Os);
            ExtrasJson.SetIfNotEmpty(obj, "app", device.App);
            ExtrasJson.SetIfNotEmpty(obj, "app_version", device.AppVersion);
            ExtrasJson.AppendUnknown(obj, device.Unknown, ExtrasKeys.Device);
            return obj;
        }

        private static JObject WriteFrame(ScanFrame frame)
        {
            var obj = new JObject { ["id"] = frame.Id ?? string.Empty };
            ExtrasJson.SetIfNotEmpty(obj, "timestamp_utc", frame.TimestampUtc);
            ExtrasJson.SetIfNotEmpty(obj, "image", frame.Image);
            if (frame.Pose != null)
            {
                obj["pose"] = WritePose(frame.Pose);
            }

            if (frame.Intrinsics != null)
            {
                obj["intrinsics"] = WriteIntrinsics(frame.Intrinsics);
            }

            ExtrasJson.AppendUnknown(obj, frame.Unknown, ExtrasKeys.Frame);
            return obj;
        }

        private static JObject WritePose(ScanPose pose)
        {
            var obj = new JObject
            {
                ["position"] = ExtrasJson.ToJson(pose.Position),
                ["quaternion"] = ExtrasJson.ToJson(pose.Rotation)
            };
            ExtrasJson.AppendUnknown(obj, pose.Unknown, ExtrasKeys.Pose);
            return obj;
        }

        private static JObject WriteIntrinsics(ScanIntrinsics intrinsics)
        {
            var obj = new JObject
            {
                ["fx"] = ExtrasJson.Finite(intrinsics.Fx),
                ["fy"] = ExtrasJson.Finite(intrinsics.Fy),
                ["cx"] = ExtrasJson.Finite(intrinsics.Cx),
                ["cy"] = ExtrasJson.Finite(intrinsics.Cy)
            };
            if (intrinsics.Width > 0)
            {
                obj["width"] = intrinsics.Width;
            }

            if (intrinsics.Height > 0)
            {
                obj["height"] = intrinsics.Height;
            }

            ExtrasJson.AppendUnknown(obj, intrinsics.Unknown, ExtrasKeys.Intrinsics);
            return obj;
        }

        private static JObject WriteBounds(ScanBounds bounds)
        {
            var obj = new JObject
            {
                ["center"] = ExtrasJson.ToJson(bounds.Center),
                ["size"] = ExtrasJson.ToJson(bounds.Size)
            };
            ExtrasJson.AppendUnknown(obj, bounds.Unknown, ExtrasKeys.Bounds);
            return obj;
        }

        private static JObject WriteObject(ScanObject value)
        {
            var obj = new JObject
            {
                ["id"] = value.Id ?? string.Empty,
                ["track_id"] = Math.Max(0, value.TrackId),
                ["label"] = WriteLabel(value.Label ?? new ScanLabel()),
                ["confidence"] = ExtrasJson.Clamp01(ExtrasJson.Finite(value.Confidence)),
                ["source"] = SourceName(value.Source),
                ["pose"] = WritePose(value.Pose ?? new ScanPose())
            };

            if (value.Normal.HasValue)
            {
                obj["normal"] = ExtrasJson.ToJson(value.Normal.Value);
            }

            if (value.Bounds != null)
            {
                obj["bounds"] = WriteBounds(value.Bounds);
            }

            ExtrasJson.SetIfNotEmpty(obj, "anchor_uuid", value.AnchorUuid);

            var observations = new JArray();
            foreach (ScanObservation observation in value.Observations)
            {
                if (observation != null)
                {
                    observations.Add(WriteObservation(observation));
                }
            }

            obj["observations"] = observations;
            ExtrasJson.AppendUnknown(obj, value.Unknown, ExtrasKeys.Obj);
            return obj;
        }

        private static JObject WriteLabel(ScanLabel label)
        {
            var obj = new JObject
            {
                ["text"] = label.Text ?? string.Empty,
                ["tag"] = label.Tag ?? string.Empty,
                ["semantic"] = MetaLabel(label.Semantic)
            };
            ExtrasJson.AppendUnknown(obj, label.Unknown, ExtrasKeys.Label);
            return obj;
        }

        private static JObject WriteObservation(ScanObservation observation)
        {
            var obj = new JObject { ["frame_id"] = observation.FrameId ?? string.Empty };
            if (observation.Box.HasValue)
            {
                var box = new JArray();
                foreach (double number in observation.Box.Value.ToCxCyWh())
                {
                    box.Add(ExtrasJson.Finite(number));
                }

                obj["bbox_cxcywh"] = box;
            }

            obj["score"] = ExtrasJson.Clamp01(ExtrasJson.Finite(observation.Score));
            ExtrasJson.AppendUnknown(obj, observation.Unknown, ExtrasKeys.Observation);
            return obj;
        }

        private static JObject WritePlane(ScanPlane plane)
        {
            var obj = new JObject
            {
                ["id"] = plane.Id ?? string.Empty,
                ["label"] = plane.Label ?? string.Empty,
                ["semantic"] = MetaLabel(plane.Semantic),
                ["pose"] = WritePose(plane.Pose ?? new ScanPose())
            };

            if (plane.Center.HasValue)
            {
                obj["center"] = ExtrasJson.ToJson(plane.Center.Value);
            }

            if (plane.Size.HasValue)
            {
                obj["size"] = ExtrasJson.ToJson(plane.Size.Value);
            }

            var boundary = new JArray();
            foreach (Vec3 point in plane.Boundary)
            {
                boundary.Add(ExtrasJson.ToJson(point));
            }

            obj["boundary"] = boundary;
            ExtrasJson.AppendUnknown(obj, plane.Unknown, ExtrasKeys.Plane);
            return obj;
        }

        private static string SourceName(ObjectSource source)
        {
            switch (source)
            {
                case ObjectSource.Device: return "device";
                case ObjectSource.Manual: return "manual";
                default: return "cv";
            }
        }

        private static string MetaLabel(string label)
        {
            return MetaLabels.IsMetaLabel(label) ? label : MetaLabels.Other;
        }
    }
}
