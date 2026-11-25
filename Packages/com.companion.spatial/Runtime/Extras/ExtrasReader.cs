using System;
using System.Globalization;
using System.IO;
using Companion.Spatial.Vocabulary;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Companion.Spatial.Extras
{
    /// <summary>
    /// Reads the extras of a scan into the v2 model: a v2 document as it is, a v1 document (what the scanner wrote
    /// until now, <c>schema_version</c> "1.0") upgraded as it is read. A key the reader does not know is never
    /// dropped: it goes to the <see cref="ExtrasNode.Unknown"/> of the part it was in, and the writer puts it back.
    /// A document that cannot be read, because it is not JSON, not an object, or not a scan's extras, gives a result
    /// with an error, never an exception.
    /// </summary>
    /// <remarks>
    /// Which version a document is: its <c>schema_version</c> (a number, or text such as "1.0" or "2"), or its
    /// <c>schema</c> id for v2; with neither, a document with <c>cv_mapping</c>, <c>pins</c> or <c>meta_scene</c> is
    /// taken as v1 (the scanner's first files had no version) with a warning. A version above 2 is read as v2 with a
    /// warning, so the keys a newer scanner added are kept. The reader is lenient inside a document: a value of the
    /// wrong type, an entry that is not an object or has no id, or a score out of range is worked around, with a
    /// warning where data was lost, and never stops the read.
    /// </remarks>
    public static class ExtrasReader
    {
        /// <summary>Reads the extras from JSON text, such as the <c>extras</c> of a glTF.</summary>
        /// <param name="json">The JSON text; a byte order mark in front is ignored.</param>
        /// <param name="vocabulary">The vocabulary that fills in a missing tag or Meta label; null means <see cref="TagVocabulary.Default"/>.</param>
        /// <returns>The result: a model, or an error saying why there is none.</returns>
        public static ExtrasReadResult Read(string json, TagVocabulary vocabulary = null)
        {
            if (json != null && json.Length > 0 && json[0] == '\uFEFF')
            {
                json = json.Substring(1);
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return ExtrasReadResult.Fail("The document is empty.");
            }

            JToken token;
            try
            {
                token = Parse(json);
            }
            catch (JsonException ex)
            {
                return ExtrasReadResult.Fail("The document is not valid JSON: " + ex.Message);
            }

            var root = token as JObject;
            if (root == null)
            {
                return ExtrasReadResult.Fail("The document is not a JSON object: it is " + ExtrasJson.Describe(token) + ".");
            }

            return ReadObject(root, vocabulary);
        }

        /// <summary>
        /// Reads the extras from a parsed JSON object. The object is not changed. Parse it with
        /// <c>DateParseHandling.None</c> to keep date-like text exactly as written: <c>JObject.Parse</c> turns such text into
        /// dates, and the reader then gives them back in ISO 8601 form (the same instant, maybe not the same text).
        /// </summary>
        /// <param name="root">The extras object; null gives an error.</param>
        /// <param name="vocabulary">The vocabulary that fills in a missing tag or Meta label; null means <see cref="TagVocabulary.Default"/>.</param>
        /// <returns>The result: a model, or an error saying why there is none.</returns>
        public static ExtrasReadResult Read(JObject root, TagVocabulary vocabulary = null)
        {
            if (root == null)
            {
                return ExtrasReadResult.Fail("The document is null.");
            }

            return ReadObject(root, vocabulary);
        }

        private static ExtrasReadResult ReadObject(JObject root, TagVocabulary vocabulary)
        {
            var context = new ReadContext(vocabulary);
            try
            {
                int version;
                if (!TryDetectVersion(root, context, out version))
                {
                    return ExtrasReadResult.Fail("The JSON object is not a Companion scan extras document: it has no usable schema_version, " +
                                                 "no schema id and none of cv_mapping, meta_scene or pins.");
                }

                ScanExtras extras = version == 1 ? ExtrasV1Reader.Read(root, context) : ExtrasV2Reader.Read(root, context);
                return ExtrasReadResult.Ok(extras, version, context.Warnings);
            }
            catch (Exception ex)
            {
                // The readers work around bad values and do not throw; this is the net for a case nobody foresaw, so
                // that a bad document is an error result, never an exception.
                return ExtrasReadResult.Fail("The document could not be read: " + ex.Message);
            }
        }

        private static JToken Parse(string json)
        {
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
            {
                JToken token = JToken.Load(reader);
                while (reader.Read())
                {
                    if (reader.TokenType != JsonToken.Comment)
                    {
                        throw new JsonReaderException("Additional text was found after the JSON value.");
                    }
                }

                return token;
            }
        }

        private static bool TryDetectVersion(JObject root, ReadContext context, out int version)
        {
            version = 0;
            int number;
            if (TryVersionNumber(root["schema_version"], out number) && number >= 1)
            {
                version = number;
                if (number > ScanExtras.CurrentVersion)
                {
                    context.Warn("schema_version " + number.ToString(CultureInfo.InvariantCulture) + " is newer than this reader's " +
                                 ScanExtras.CurrentVersion.ToString(CultureInfo.InvariantCulture) + "; it was read as v2 and the keys it does not know were kept");
                }

                return true;
            }

            if (string.Equals(ExtrasJson.AsString(root["schema"]), ScanExtras.SchemaId, StringComparison.Ordinal))
            {
                version = ScanExtras.CurrentVersion;
                return true;
            }

            if (root.ContainsKey("cv_mapping") || root.ContainsKey("pins") || root.ContainsKey("meta_scene"))
            {
                version = 1;
                context.Warn("the document has no usable schema_version; it looks like a v1 file and was read as one");
                return true;
            }

            return false;
        }

        // 1, 1.0 and "1.0" are version 1; "2" and 2.0 are version 2.
        private static bool TryVersionNumber(JToken token, out int number)
        {
            number = 0;
            if (token == null)
            {
                return false;
            }

            double value;
            if (ExtrasJson.TryDouble(token, out value))
            {
                number = (int)Math.Floor(value);
                return true;
            }

            string text = ExtrasJson.AsString(token);
            if (text == null)
            {
                return false;
            }

            text = text.Trim();
            int dot = text.IndexOf('.');
            if (dot >= 0)
            {
                text = text.Substring(0, dot);
            }

            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
        }
    }
}
