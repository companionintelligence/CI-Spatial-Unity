// A small purpose-built structural checker for the JSON Schema of extras v2 (draft 2020-12), so the tests need no
// schema package. It supports only the keywords the schema uses and refuses to run against a schema that uses
// another one, so the schema and the checker cannot drift apart unnoticed.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Companion.Spatial.Tests
{
    internal static class MiniSchema
    {
        // Keywords that only describe the schema.
        private static readonly HashSet<string> Annotations = new HashSet<string>
        {
            "$schema", "$id", "$comment", "$defs", "title", "description", "default", "examples"
        };

        // Keywords that are checked.
        private static readonly HashSet<string> Checked = new HashSet<string>
        {
            "type", "const", "enum", "required", "properties", "additionalProperties", "items", "minItems", "maxItems",
            "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "minLength", "maxLength", "format", "$ref"
        };

        /// <summary>Every problem the instance has against the schema, each with the path of the value; empty when it is valid.</summary>
        internal static List<string> Validate(JToken instance, JObject schema)
        {
            var errors = new List<string>();
            ValidateNode(instance, schema, schema, "$", errors);
            return errors;
        }

        /// <summary>
        /// The paths of keys in the instance that the schema does not declare under "properties" (the schema allows
        /// them, but a writer that emits one the schema never mentions has drifted from it).
        /// </summary>
        internal static List<string> UndeclaredKeys(JToken instance, JObject schema)
        {
            var found = new List<string>();
            Declared(instance, schema, schema, "$", found);
            return found;
        }

        /// <summary>The keywords in the schema that the checker does not know (and so would not check), with their paths.</summary>
        internal static List<string> UnsupportedKeywords(JToken schema)
        {
            var found = new List<string>();
            Walk(schema, "$", found);
            return found;
        }

        private static void Walk(JToken node, string path, List<string> found)
        {
            var obj = node as JObject;
            if (obj == null)
            {
                return;
            }

            foreach (var property in obj.Properties())
            {
                string name = property.Name;
                if (!Annotations.Contains(name) && !Checked.Contains(name))
                {
                    found.Add(path + "." + name);
                    continue;
                }

                if (name == "properties" || name == "$defs")
                {
                    // the children are named schemas; "properties" may also hold a property called like a keyword
                    foreach (var child in ((JObject)property.Value).Properties())
                    {
                        Walk(child.Value, path + "." + name + "." + child.Name, found);
                    }
                }
                else if (name == "items" || name == "additionalProperties")
                {
                    Walk(property.Value, path + "." + name, found);
                }
            }
        }

        private static void Declared(JToken instance, JObject schema, JObject root, string path, List<string> found)
        {
            JToken reference;
            if (schema.TryGetValue("$ref", out reference))
            {
                schema = Resolve((string)reference, root);
            }

            var obj = instance as JObject;
            var properties = schema["properties"] as JObject;
            if (obj != null && properties != null)
            {
                foreach (var property in obj.Properties())
                {
                    JToken declared = properties[property.Name];
                    if (declared == null)
                    {
                        found.Add(path + "." + property.Name);
                    }
                    else
                    {
                        Declared(property.Value, (JObject)declared, root, path + "." + property.Name, found);
                    }
                }
            }

            var array = instance as JArray;
            var items = schema["items"] as JObject;
            if (array != null && items != null)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    Declared(array[i], items, root, path + "[" + i + "]", found);
                }
            }
        }

        private static void ValidateNode(JToken instance, JObject schema, JObject root, string path, List<string> errors)
        {
            JToken reference;
            if (schema.TryGetValue("$ref", out reference))
            {
                JObject target = Resolve((string)reference, root);
                ValidateNode(instance, target, root, path, errors);
            }

            JToken type;
            if (schema.TryGetValue("type", out type) && !MatchesType(instance, type))
            {
                errors.Add(path + ": expected type " + type.ToString(Newtonsoft.Json.Formatting.None) + " but is " + Describe(instance));
                return;
            }

            JToken constant;
            if (schema.TryGetValue("const", out constant) && !SameValue(constant, instance))
            {
                errors.Add(path + ": must be " + constant.ToString(Newtonsoft.Json.Formatting.None) + " but is " + instance.ToString(Newtonsoft.Json.Formatting.None));
            }

            JToken allowed;
            if (schema.TryGetValue("enum", out allowed) && !((JArray)allowed).Any(option => SameValue(option, instance)))
            {
                errors.Add(path + ": " + instance.ToString(Newtonsoft.Json.Formatting.None) + " is not one of " + allowed.ToString(Newtonsoft.Json.Formatting.None));
            }

            CheckNumber(instance, schema, path, errors);
            CheckString(instance, schema, path, errors);

            var obj = instance as JObject;
            if (obj != null)
            {
                CheckObject(obj, schema, root, path, errors);
            }

            var array = instance as JArray;
            if (array != null)
            {
                CheckArray(array, schema, root, path, errors);
            }
        }

        private static void CheckObject(JObject obj, JObject schema, JObject root, string path, List<string> errors)
        {
            JToken required;
            if (schema.TryGetValue("required", out required))
            {
                foreach (string name in required.Select(item => (string)item))
                {
                    if (!obj.ContainsKey(name))
                    {
                        errors.Add(path + ": missing the required key \"" + name + "\"");
                    }
                }
            }

            var properties = schema["properties"] as JObject;
            JToken additional;
            schema.TryGetValue("additionalProperties", out additional);
            foreach (var property in obj.Properties())
            {
                JToken known = properties == null ? null : properties[property.Name];
                if (known != null)
                {
                    ValidateNode(property.Value, (JObject)known, root, path + "." + property.Name, errors);
                }
                else if (additional != null && additional.Type == JTokenType.Boolean && !(bool)additional)
                {
                    errors.Add(path + ": the key \"" + property.Name + "\" is not allowed");
                }
                else if (additional is JObject)
                {
                    ValidateNode(property.Value, (JObject)additional, root, path + "." + property.Name, errors);
                }
            }
        }

        private static void CheckArray(JArray array, JObject schema, JObject root, string path, List<string> errors)
        {
            JToken min;
            if (schema.TryGetValue("minItems", out min) && array.Count < (int)min)
            {
                errors.Add(path + ": has " + array.Count + " items, at least " + (int)min + " needed");
            }

            JToken max;
            if (schema.TryGetValue("maxItems", out max) && array.Count > (int)max)
            {
                errors.Add(path + ": has " + array.Count + " items, at most " + (int)max + " allowed");
            }

            var items = schema["items"] as JObject;
            if (items != null)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    ValidateNode(array[i], items, root, path + "[" + i + "]", errors);
                }
            }
        }

        private static void CheckNumber(JToken instance, JObject schema, string path, List<string> errors)
        {
            if (instance.Type != JTokenType.Integer && instance.Type != JTokenType.Float)
            {
                return;
            }

            double value = (double)instance;
            JToken limit;
            if (schema.TryGetValue("minimum", out limit) && value < (double)limit)
            {
                errors.Add(path + ": " + value.ToString(CultureInfo.InvariantCulture) + " is below the minimum " + limit);
            }

            if (schema.TryGetValue("maximum", out limit) && value > (double)limit)
            {
                errors.Add(path + ": " + value.ToString(CultureInfo.InvariantCulture) + " is above the maximum " + limit);
            }

            if (schema.TryGetValue("exclusiveMinimum", out limit) && value <= (double)limit)
            {
                errors.Add(path + ": " + value.ToString(CultureInfo.InvariantCulture) + " must be above " + limit);
            }

            if (schema.TryGetValue("exclusiveMaximum", out limit) && value >= (double)limit)
            {
                errors.Add(path + ": " + value.ToString(CultureInfo.InvariantCulture) + " must be below " + limit);
            }
        }

        private static void CheckString(JToken instance, JObject schema, string path, List<string> errors)
        {
            if (instance.Type != JTokenType.String)
            {
                return;
            }

            string text = (string)instance;
            JToken limit;
            if (schema.TryGetValue("minLength", out limit) && text.Length < (int)limit)
            {
                errors.Add(path + ": \"" + text + "\" is shorter than " + (int)limit);
            }

            if (schema.TryGetValue("maxLength", out limit) && text.Length > (int)limit)
            {
                errors.Add(path + ": \"" + text + "\" is longer than " + (int)limit);
            }

            JToken format;
            if (schema.TryGetValue("format", out format) && (string)format == "date-time")
            {
                DateTimeOffset parsed;
                if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed) || text.IndexOf('T') < 0)
                {
                    errors.Add(path + ": \"" + text + "\" is not an ISO 8601 date-time");
                }
            }
        }

        private static JObject Resolve(string reference, JObject root)
        {
            if (!reference.StartsWith("#/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Only references inside the document are supported: " + reference);
            }

            JToken node = root;
            foreach (string part in reference.Substring(2).Split('/'))
            {
                node = ((JObject)node)[part];
                if (node == null)
                {
                    throw new InvalidOperationException("The reference " + reference + " leads nowhere.");
                }
            }

            return (JObject)node;
        }

        private static bool MatchesType(JToken instance, JToken type)
        {
            if (type.Type == JTokenType.Array)
            {
                return type.Any(option => MatchesType(instance, option));
            }

            switch ((string)type)
            {
                case "object": return instance.Type == JTokenType.Object;
                case "array": return instance.Type == JTokenType.Array;
                case "string": return instance.Type == JTokenType.String;
                case "boolean": return instance.Type == JTokenType.Boolean;
                case "null": return instance.Type == JTokenType.Null;
                case "number": return instance.Type == JTokenType.Integer || instance.Type == JTokenType.Float;
                case "integer":
                    return instance.Type == JTokenType.Integer ||
                           (instance.Type == JTokenType.Float && Math.Abs((double)instance % 1.0) < 1e-12);
                default: throw new InvalidOperationException("Unknown type " + type);
            }
        }

        private static bool SameValue(JToken a, JToken b)
        {
            if ((a.Type == JTokenType.Integer || a.Type == JTokenType.Float) && (b.Type == JTokenType.Integer || b.Type == JTokenType.Float))
            {
                return (double)a == (double)b;
            }

            return JToken.DeepEquals(a, b);
        }

        private static string Describe(JToken instance)
        {
            return instance.Type.ToString().ToLowerInvariant();
        }
    }
}
