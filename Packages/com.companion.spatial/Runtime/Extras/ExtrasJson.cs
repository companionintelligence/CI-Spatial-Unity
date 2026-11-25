using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Companion.Spatial.Extras
{
    /// <summary>
    /// The small pieces of JSON reading and writing the reader and the writer share: numbers that may be missing or
    /// the wrong type, vectors as arrays or as objects, and the unknown keys. Nothing here throws on bad input.
    /// </summary>
    internal static class ExtrasJson
    {
        // ---- reading ----

        /// <summary>
        /// The text of a string token, of a date token in ISO 8601 form, or of a number or boolean written out (1.4 becomes
        /// "1.4", so a version number that was written as a number is not lost); null for anything else.
        /// </summary>
        internal static string AsString(JToken token)
        {
            if (token == null)
            {
                return null;
            }

            if (token.Type == JTokenType.String)
            {
                return (string)token;
            }

            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float || token.Type == JTokenType.Boolean)
            {
                object number = ((JValue)token).Value;
                if (number is double && !double.IsFinite((double)number))
                {
                    return null;
                }

                var formattable = number as IFormattable;
                return formattable == null ? number.ToString().ToLowerInvariant() : formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            if (token.Type == JTokenType.Date)
            {
                object value = ((JValue)token).Value;
                if (value is DateTime)
                {
                    return ((DateTime)value).ToString("o", CultureInfo.InvariantCulture);
                }

                if (value is DateTimeOffset)
                {
                    return ((DateTimeOffset)value).ToString("o", CultureInfo.InvariantCulture);
                }
            }

            return null;
        }

        /// <summary>A real number (JSON integer or float); false for anything else, NaN and infinity included.</summary>
        internal static bool TryDouble(JToken token, out double value)
        {
            value = 0.0;
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
            {
                return false;
            }

            try
            {
                value = (double)token;
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException || ex is ArgumentException)
            {
                value = 0.0;
                return false;
            }

            return double.IsFinite(value);
        }

        /// <summary>A whole number: a JSON integer, a float with no fraction, or text that holds one ("7").</summary>
        internal static bool TryInt(JToken token, out int value)
        {
            value = 0;
            if (token == null)
            {
                return false;
            }

            if (token.Type == JTokenType.String)
            {
                return int.TryParse(((string)token).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }

            double number;
            if (!TryDouble(token, out number) || number != Math.Floor(number) || number < int.MinValue || number > int.MaxValue)
            {
                return false;
            }

            value = (int)number;
            return true;
        }

        /// <summary>Three numbers as [x, y, z] or as { "x", "y", "z" }.</summary>
        internal static bool TryVec3(JToken token, out Vec3 value)
        {
            value = Vec3.Zero;
            double x, y, z;
            var array = token as JArray;
            if (array != null)
            {
                if (array.Count != 3 || !TryDouble(array[0], out x) || !TryDouble(array[1], out y) || !TryDouble(array[2], out z))
                {
                    return false;
                }
            }
            else
            {
                var obj = token as JObject;
                if (obj == null || !TryDouble(obj["x"], out x) || !TryDouble(obj["y"], out y) || !TryDouble(obj["z"], out z))
                {
                    return false;
                }
            }

            value = new Vec3(x, y, z);
            return true;
        }

        /// <summary>Two numbers as [x, y] (more are ignored, so a v1 [x, y, 0] reads) or as { "x", "y" }.</summary>
        internal static bool TryVec2(JToken token, out Vec2 value)
        {
            value = default;
            double x, y;
            var array = token as JArray;
            if (array != null)
            {
                if (array.Count < 2 || !TryDouble(array[0], out x) || !TryDouble(array[1], out y))
                {
                    return false;
                }
            }
            else
            {
                var obj = token as JObject;
                if (obj == null || !TryDouble(obj["x"], out x) || !TryDouble(obj["y"], out y))
                {
                    return false;
                }
            }

            value = new Vec2(x, y);
            return true;
        }

        /// <summary>Four numbers as [x, y, z, w] or as { "x", "y", "z", "w" }.</summary>
        internal static bool TryQuat(JToken token, out Quat value)
        {
            value = Quat.Identity;
            double x, y, z, w;
            var array = token as JArray;
            if (array != null)
            {
                if (array.Count != 4 || !TryDouble(array[0], out x) || !TryDouble(array[1], out y) || !TryDouble(array[2], out z) || !TryDouble(array[3], out w))
                {
                    return false;
                }
            }
            else
            {
                var obj = token as JObject;
                if (obj == null || !TryDouble(obj["x"], out x) || !TryDouble(obj["y"], out y) || !TryDouble(obj["z"], out z) || !TryDouble(obj["w"], out w))
                {
                    return false;
                }
            }

            value = new Quat(x, y, z, w);
            return true;
        }

        /// <summary>True when the token is there and is not a JSON null.</summary>
        internal static bool IsPresent(JToken token)
        {
            return token != null && token.Type != JTokenType.Null;
        }

        /// <summary>What a token is, for a warning: "a list", "text", "a number", ...</summary>
        internal static string Describe(JToken token)
        {
            if (token == null)
            {
                return "missing";
            }

            switch (token.Type)
            {
                case JTokenType.Array: return "a list";
                case JTokenType.Object: return "an object";
                case JTokenType.String: return "text";
                case JTokenType.Integer:
                case JTokenType.Float: return "a number";
                case JTokenType.Boolean: return "true or false";
                case JTokenType.Null: return "null";
                case JTokenType.Date: return "a date";
                default: return "a " + token.Type.ToString().ToLowerInvariant();
            }
        }

        internal static double Clamp01(double value)
        {
            return value < 0.0 ? 0.0 : (value > 1.0 ? 1.0 : value);
        }

        internal static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>Copies every key of <paramref name="source"/> that is not in <paramref name="known"/> into the node's unknown keys.</summary>
        internal static void KeepUnknown(JObject source, ExtrasNode node, params string[] known)
        {
            foreach (JProperty property in source.Properties())
            {
                if (Array.IndexOf(known, property.Name) < 0)
                {
                    node.Unknown[property.Name] = property.Value.DeepClone();
                }
            }
        }

        // ---- writing ----

        /// <summary>The number, or 0 when it is NaN or infinite (JSON has no such numbers).</summary>
        internal static double Finite(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }

        internal static JObject ToJson(Vec3 value)
        {
            return new JObject { ["x"] = Finite(value.X), ["y"] = Finite(value.Y), ["z"] = Finite(value.Z) };
        }

        internal static JObject ToJson(Vec2 value)
        {
            return new JObject { ["x"] = Finite(value.X), ["y"] = Finite(value.Y) };
        }

        internal static JObject ToJson(Quat value)
        {
            return new JObject { ["x"] = Finite(value.X), ["y"] = Finite(value.Y), ["z"] = Finite(value.Z), ["w"] = Finite(value.W) };
        }

        /// <summary>Sets a string member when there is something to write.</summary>
        internal static void SetIfNotEmpty(JObject target, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                target[key] = value;
            }
        }

        /// <summary>
        /// Adds a node's unknown keys after the known ones, as copies. A key the object already has is not overwritten, and
        /// a key the part reserves (<paramref name="reserved"/>, see <see cref="ExtrasKeys"/>) is not written even when the
        /// part left it out.
        /// </summary>
        internal static void AppendUnknown(JObject target, JObject unknown, string[] reserved)
        {
            if (unknown == null)
            {
                return;
            }

            foreach (JProperty property in unknown.Properties())
            {
                if (!target.ContainsKey(property.Name) && Array.IndexOf(reserved, property.Name) < 0)
                {
                    target[property.Name] = property.Value.DeepClone();
                }
            }
        }
    }
}
