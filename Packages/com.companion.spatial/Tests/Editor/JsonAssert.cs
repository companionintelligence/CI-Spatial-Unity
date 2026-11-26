// Compares JSON the way the files are read: numbers by value (1 and 1.0 are the same), strings as strings, and every
// difference listed with its path.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Companion.Spatial.Tests
{
    internal static class JsonAssert
    {
        private const double Tolerance = 1e-9;

        /// <summary>Reads JSON text without turning date-like strings into dates (JObject.Parse would).</summary>
        internal static JToken Parse(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
            {
                return JToken.Load(reader);
            }
        }

        internal static JObject ParseObject(string json)
        {
            return (JObject)Parse(json);
        }

        /// <summary>Fails with every difference between the two trees; the order of an object's keys does not matter.</summary>
        internal static void AreSame(JToken expected, JToken actual, string what)
        {
            var differences = new List<string>();
            Compare(expected, actual, "$", differences);

            Assert.IsEmpty(differences, what + ": " + string.Join("; ", differences) + "\nactual:\n" + (actual == null ? "nothing" : actual.ToString()));
        }

        /// <summary>The key names of an object, in the order they appear.</summary>
        internal static string[] Keys(JToken token)
        {
            return ((JObject)token).Properties().Select(property => property.Name).ToArray();
        }

        /// <summary>The paths of every JSON null in the tree.</summary>
        internal static List<string> Nulls(JToken token)
        {
            var found = new List<string>();
            FindNulls(token, found);
            return found;
        }

        private static void FindNulls(JToken token, List<string> found)
        {
            if (token.Type == JTokenType.Null)
            {
                found.Add(token.Path);
                return;
            }

            foreach (var child in token.Children())
            {
                FindNulls(child, found);
            }
        }

        private static void Compare(JToken expected, JToken actual, string path, List<string> differences)
        {
            if (expected == null || actual == null)
            {
                if (expected != actual)
                {
                    differences.Add(path + ": expected " + Show(expected) + " but was " + Show(actual));
                }

                return;
            }

            if (IsNumber(expected) && IsNumber(actual))
            {
                if (Math.Abs((double)expected - (double)actual) > Tolerance)
                {
                    differences.Add(path + ": expected " + Show(expected) + " but was " + Show(actual));
                }

                return;
            }

            if (expected.Type != actual.Type)
            {
                differences.Add(path + ": expected " + Show(expected) + " but was " + Show(actual));
                return;
            }

            switch (expected.Type)
            {
                case JTokenType.Object:
                    var expectedObject = (JObject)expected;
                    var actualObject = (JObject)actual;
                    foreach (var property in expectedObject.Properties())
                    {
                        Compare(property.Value, actualObject[property.Name], path + "." + property.Name, differences);
                    }

                    foreach (var property in actualObject.Properties())
                    {
                        if (expectedObject[property.Name] == null)
                        {
                            differences.Add(path + "." + property.Name + ": not expected, but it is " + Show(property.Value));
                        }
                    }

                    break;
                case JTokenType.Array:
                    var expectedArray = (JArray)expected;
                    var actualArray = (JArray)actual;
                    if (expectedArray.Count != actualArray.Count)
                    {
                        differences.Add(path + ": expected " + expectedArray.Count + " items but there are " + actualArray.Count);
                    }

                    for (int i = 0; i < Math.Min(expectedArray.Count, actualArray.Count); i++)
                    {
                        Compare(expectedArray[i], actualArray[i], path + "[" + i + "]", differences);
                    }

                    break;
                default:
                    if (!JToken.DeepEquals(expected, actual))
                    {
                        differences.Add(path + ": expected " + Show(expected) + " but was " + Show(actual));
                    }

                    break;
            }
        }

        private static bool IsNumber(JToken token)
        {
            return token.Type == JTokenType.Integer || token.Type == JTokenType.Float;
        }

        private static string Show(JToken token)
        {
            return token == null ? "nothing" : token.ToString(Formatting.None);
        }
    }
}
