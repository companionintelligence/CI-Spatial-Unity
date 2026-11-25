using System;
using System.IO;
using System.Linq;
using System.Text;
using CompanionScan.Export;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// Characterization tests: they record what <see cref="GltfExtrasWriter"/> does to a glTF file
    /// today. A test with "Currently" in its name records a result that looks doubtful, not a
    /// requirement.
    /// </summary>
    public class GltfExtrasWriterCharacterizationTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "CompanionScanTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void WriteExtras_AddsTheExtrasAfterTheOtherFieldsOfTheRootAndKeepsThoseFieldsAsTheyWere()
        {
            var path = WriteGltf(@"{
  ""asset"": { ""version"": ""2.0"", ""generator"": ""test"" },
  ""scene"": 0,
  ""scenes"": [ { ""nodes"": [ 0 ] } ],
  ""nodes"": [ { ""name"": ""FloorplanMesh"" } ],
  ""buffers"": [ { ""uri"": ""model.bin"", ""byteLength"": 4 } ]
}");
            var before = JObject.Parse(File.ReadAllText(path));
            var extras = JObject.Parse(@"{
  ""units"": ""meters"",
  ""pins"": [ { ""label"": ""chair"", ""score"": 0.5, ""track_id"": 7 } ],
  ""nested"": { ""a"": [ 1, 2, 3 ] },
  ""flag"": true,
  ""none"": null
}");

            GltfExtrasWriter.WriteExtras(path, extras);

            var after = JObject.Parse(File.ReadAllText(path));
            CollectionAssert.AreEqual(new[] { "asset", "scene", "scenes", "nodes", "buffers", "extras" },
                after.Properties().Select(property => property.Name).ToArray());
            foreach (var property in before.Properties())
            {
                Assert.IsTrue(JToken.DeepEquals(property.Value, after[property.Name]), $"\"{property.Name}\" changed");
            }

            Assert.IsTrue(JToken.DeepEquals(extras, after["extras"]), after["extras"].ToString());
        }

        [Test]
        public void WriteExtras_ReplacesTheExtrasAlreadyInTheFile_WithoutMergingAndWithoutMovingThem()
        {
            var path = WriteGltf(@"{ ""extras"": { ""old"": 1, ""pins"": [ { ""label"": ""a"" } ] }, ""asset"": { ""version"": ""2.0"" } }");

            GltfExtrasWriter.WriteExtras(path, JObject.Parse(@"{ ""new"": 2 }"));

            var after = JObject.Parse(File.ReadAllText(path));
            CollectionAssert.AreEqual(new[] { "extras", "asset" }, after.Properties().Select(property => property.Name).ToArray());
            Assert.IsTrue(JToken.DeepEquals(JObject.Parse(@"{ ""new"": 2 }"), after["extras"]), after["extras"].ToString());
        }

        [Test]
        public void WriteExtras_WritesIndentedUtf8WithoutAByteOrderMarkOrATrailingNewline()
        {
            var path = WriteGltf(@"{""asset"":{""version"":""2.0""}}");

            GltfExtrasWriter.WriteExtras(path, new JObject { ["units"] = "meters" });

            var bytes = File.ReadAllBytes(path);
            Assert.AreEqual((byte)'{', bytes[0], "the file starts with a byte order mark");
            var text = Encoding.UTF8.GetString(bytes);
            StringAssert.Contains("\n  \"asset\": {", text);
            StringAssert.Contains("\n    \"version\": \"2.0\"", text);
            StringAssert.EndsWith("}", text);
        }

        [Test]
        public void WriteExtras_KeepsNonAsciiTextAsUtf8_NotAsEscapes()
        {
            var path = WriteGltf(@"{ ""asset"": { ""version"": ""2.0"" } }");

            GltfExtrasWriter.WriteExtras(path, new JObject { ["label"] = "Küche" });

            var text = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("\"label\": \"Küche\"", text);
            StringAssert.DoesNotContain("\\u00", text);
        }

        [Test]
        public void WriteExtras_WritesAFloatWithItsShortestFormAndAWholeFloatWithADecimalPoint()
        {
            var path = WriteGltf(@"{ ""asset"": { ""version"": ""2.0"" } }");

            GltfExtrasWriter.WriteExtras(path, new JObject
            {
                ["x"] = 0.1f,
                ["y"] = 1f,
                ["count"] = 3,
                ["time"] = 12.5d
            });

            var text = File.ReadAllText(path);
            Assert.That(text, Does.Match("\"x\": 0\\.1(,|\\s)"), text);
            Assert.That(text, Does.Match("\"y\": 1\\.0(,|\\s)"), text);
            Assert.That(text, Does.Match("\"count\": 3(,|\\s)"), text);
            Assert.That(text, Does.Match("\"time\": 12\\.5(,|\\s)"), text);
        }

        [Test]
        public void WriteExtras_WithEmptyExtras_WritesAnEmptyExtrasObject()
        {
            var path = WriteGltf(@"{ ""asset"": { ""version"": ""2.0"" } }");

            GltfExtrasWriter.WriteExtras(path, new JObject());

            var extras = JObject.Parse(File.ReadAllText(path))["extras"];
            Assert.IsInstanceOf<JObject>(extras);
            Assert.IsFalse(extras.HasValues);
        }

        [Test]
        public void WriteExtras_WithNullExtras_LeavesTheFileUntouched()
        {
            var path = WriteGltf("{\"asset\":{\"version\":\"2.0\"}}  ");
            var before = File.ReadAllBytes(path);

            GltfExtrasWriter.WriteExtras(path, null);

            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }

        [Test]
        public void WriteExtras_WithAMissingFile_DoesNothingAndDoesNotCreateTheFile()
        {
            var path = Path.Combine(_directory, "missing.gltf");

            Assert.DoesNotThrow(() => GltfExtrasWriter.WriteExtras(path, new JObject { ["units"] = "meters" }));

            Assert.IsFalse(File.Exists(path));
            Assert.IsEmpty(Directory.GetFileSystemEntries(_directory));
        }

        [TestCase(null)]
        [TestCase("")]
        public void WriteExtras_WithoutAPath_DoesNothing(string path)
        {
            Assert.DoesNotThrow(() => GltfExtrasWriter.WriteExtras(path, new JObject { ["units"] = "meters" }));
            Assert.IsEmpty(Directory.GetFileSystemEntries(_directory));
        }

        [Test]
        public void WriteExtras_LogsThePathOfTheFileItWrote()
        {
            var path = WriteGltf(@"{ ""asset"": { ""version"": ""2.0"" } }");

            LogAssert.Expect(LogType.Log, "Wrote glTF extras: " + path);
            GltfExtrasWriter.WriteExtras(path, new JObject { ["units"] = "meters" });
        }

        // A file that is not a JSON object makes the write throw (so the export fails) and is left as it was.
        [TestCase("not json")]
        [TestCase("")]
        [TestCase("[]")]
        public void WriteExtras_Currently_ThrowsForAFileThatIsNotAJsonObject_AndLeavesItAsItWas(string content)
        {
            var path = WriteGltf(content);

            Assert.Catch<JsonException>(() => GltfExtrasWriter.WriteExtras(path, new JObject { ["units"] = "meters" }));

            Assert.AreEqual(content, File.ReadAllText(path));
        }

        private string WriteGltf(string content)
        {
            var path = Path.Combine(_directory, "model.gltf");
            File.WriteAllText(path, content);
            return path;
        }
    }
}
