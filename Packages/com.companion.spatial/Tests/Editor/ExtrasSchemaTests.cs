// Tests that the writer's output satisfies Documentation~/scan-extras.v2.schema.json (required keys and types, by the
// small checker in MiniSchema), that the checker is strict enough for that to mean something, and that v1 files
// upgraded to v2 validate too.

using System;
using System.Collections.Generic;
using System.Linq;
using Companion.Spatial.Extras;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Companion.Spatial.Tests
{
    public class ExtrasSchemaTests
    {
        private static JObject Schema()
        {
            return JsonAssert.ParseObject(TestPaths.ReadSchema());
        }

        private static List<string> Validate(JToken document)
        {
            return MiniSchema.Validate(document, Schema());
        }

        // ---- the schema file ----

        [Test]
        public void TheSchemaFile_IsDraft2020_12_AndUsesOnlyKeywordsTheCheckerKnows()
        {
            var schema = Schema();

            Assert.AreEqual("https://json-schema.org/draft/2020-12/schema", (string)schema["$schema"]);
            Assert.AreEqual("object", (string)schema["type"]);
            Assert.IsEmpty(MiniSchema.UnsupportedKeywords(schema), "a keyword the checker does not check would make the schema tests pass for nothing");
        }

        [Test]
        public void TheSchema_RequiresTheKeysTheWriterAlwaysWrites()
        {
            var schema = Schema();

            CollectionAssert.AreEquivalent(new[] { "schema", "schema_version", "units", "space", "frames", "objects", "planes" },
                schema["required"].Select(item => (string)item).ToArray());
            Assert.AreEqual("companion.scan-extras", (string)schema["properties"]["schema"]["const"]);
            Assert.AreEqual(2, (int)schema["properties"]["schema_version"]["const"]);
            Assert.AreEqual("meters", (string)schema["properties"]["units"]["const"]);
        }

        [Test]
        public void TheSchema_DescribesEveryPartOfTheModel()
        {
            var defs = (JObject)Schema()["$defs"];

            foreach (var name in new[] { "vec2", "vec3", "quat", "pose", "bounds", "box", "intrinsics", "label", "observation", "object", "frame", "plane", "space", "consent", "device", "metaLabel", "timestamp" })
            {
                Assert.IsNotNull(defs[name], "$defs." + name);
            }

            CollectionAssert.AreEqual(new[]
            {
                "CEILING", "DOOR_FRAME", "FLOOR", "INVISIBLE_WALL_FACE", "WALL_ART", "WALL_FACE", "WINDOW_FRAME", "COUCH",
                "TABLE", "BED", "LAMP", "PLANT", "SCREEN", "STORAGE", "GLOBAL_MESH", "OTHER"
            }, defs["metaLabel"]["enum"].Select(item => (string)item).ToArray());
        }

        // ---- the writer's output ----

        [Test]
        public void TheWritersOutput_SatisfiesTheSchema()
        {
            foreach (var pair in Models.ValidModels())
            {
                var errors = Validate(ExtrasWriter.Write(pair.Value));

                Assert.IsEmpty(errors, pair.Key + ": " + string.Join("; ", errors));
            }
        }

        [Test]
        public void EveryKeyTheWriterWrites_IsDeclaredInTheSchema()
        {
            foreach (var pair in Models.ValidModels())
            {
                var undeclared = MiniSchema.UndeclaredKeys(ExtrasWriter.Write(pair.Value), Schema());

                Assert.IsEmpty(undeclared, pair.Key + ": the schema does not mention " + string.Join(", ", undeclared));
            }
        }

        [Test]
        public void AV1FileUpgradedToV2_SatisfiesTheSchema()
        {
            // the golden file holds the placeholder "<timestamp_utc>" where the exporter writes the time
            var golden = Models.GoldenV1();
            golden["timestamp_utc"] = "2026-11-25T10:00:00.1234567Z";

            foreach (var json in new[] { golden, Models.ExporterShapedV1() })
            {
                var extras = ExtrasReader.Read(json).Extras;

                var errors = Validate(ExtrasWriter.Write(extras));

                Assert.IsEmpty(errors, string.Join("; ", errors));
            }
        }

        // The reader keeps a time as it was written, even when it is not a time; the schema is what flags it.
        [Test]
        public void ATimeThatIsNotATime_IsKeptByTheReader_AndFlaggedBySchema()
        {
            var extras = ExtrasReader.Read(Models.GoldenV1()).Extras;
            Assert.AreEqual("<timestamp_utc>", extras.Space.UpdatedAtUtc);

            var errors = Validate(ExtrasWriter.Write(extras));

            Assert.AreEqual(1, errors.Count, string.Join("; ", errors));
            StringAssert.Contains("updated_at_utc", errors[0]);
        }

        [Test]
        public void TheHandWrittenV2Document_SatisfiesTheSchema()
        {
            var errors = Validate(JsonAssert.Parse(Models.V2Document));

            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [Test]
        public void AMergedDocument_SatisfiesTheSchema()
        {
            var merged = ExtrasMerge.MergeFromObject(Models.ExporterShapedV1(), Models.Full()).Merged;

            var errors = Validate(ExtrasWriter.Write(merged));

            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        // ---- the schema has teeth ----

        private static IEnumerable<TestCaseData> Mutations()
        {
            yield return Case("a missing objects list", d => d.Remove("objects"), "\"objects\"");
            yield return Case("a missing space", d => d.Remove("space"), "\"space\"");
            yield return Case("a missing schema id", d => d.Remove("schema"), "\"schema\"");
            yield return Case("another schema version", d => d["schema_version"] = 3, "schema_version");
            yield return Case("the schema version as text", d => d["schema_version"] = "2", "schema_version");
            yield return Case("other units", d => d["units"] = "feet", "units");
            yield return Case("a space without an id", d => ((JObject)d["space"]).Remove("id"), "\"id\"");
            yield return Case("a bad time", d => d["space"]["updated_at_utc"] = "yesterday", "updated_at_utc");
            yield return Case("consent without a time", d => ((JObject)d["consent"]).Remove("agreed_at_utc"), "\"agreed_at_utc\"");
            yield return Case("a confidence as text", d => d["objects"][0]["confidence"] = "high", "confidence");
            yield return Case("a confidence above 1", d => d["objects"][0]["confidence"] = 1.5, "confidence");
            yield return Case("a negative track id", d => d["objects"][0]["track_id"] = -1, "track_id");
            yield return Case("an unknown source", d => d["objects"][0]["source"] = "robot", "source");
            yield return Case("an unknown Meta label", d => d["objects"][0]["label"]["semantic"] = "CHAIR", "semantic");
            yield return Case("a label without a tag", d => ((JObject)d["objects"][0]["label"]).Remove("tag"), "\"tag\"");
            yield return Case("an object without a pose", d => ((JObject)d["objects"][0]).Remove("pose"), "\"pose\"");
            yield return Case("a position as text", d => d["objects"][0]["pose"]["position"]["x"] = "1", "position");
            yield return Case("a quaternion without w", d => ((JObject)d["objects"][0]["pose"]["quaternion"]).Remove("w"), "\"w\"");
            yield return Case("an observation without a frame", d => ((JObject)d["objects"][0]["observations"][0]).Remove("frame_id"), "\"frame_id\"");
            yield return Case("a box of three numbers", d => d["objects"][0]["observations"][0]["bbox_cxcywh"] = new JArray(0.1, 0.2, 0.3), "bbox_cxcywh");
            yield return Case("a box past the image", d => d["objects"][0]["observations"][0]["bbox_cxcywh"] = new JArray(0.5, 0.5, 1.5, 0.2), "bbox_cxcywh");
            yield return Case("a score above 1", d => d["objects"][0]["observations"][0]["score"] = 2, "score");
            yield return Case("bounds without a size", d => ((JObject)d["objects"][1]["bounds"]).Remove("size"), "\"size\"");
            yield return Case("a frame without an id", d => ((JObject)d["frames"][0]).Remove("id"), "\"id\"");
            yield return Case("a focal length of 0", d => d["frames"][0]["intrinsics"]["fx"] = 0, "fx");
            yield return Case("an image width of 0", d => d["frames"][0]["intrinsics"]["width"] = 0, "width");
            yield return Case("intrinsics without a principal point", d => ((JObject)d["frames"][0]["intrinsics"]).Remove("cx"), "\"cx\"");
            yield return Case("a plane without a boundary list", d => ((JObject)d["planes"][0]).Remove("boundary"), "\"boundary\"");
            yield return Case("a plane size as an array", d => d["planes"][0]["size"] = new JArray(4, 2.5), "size");
            yield return Case("a plane without a Meta label", d => ((JObject)d["planes"][0]).Remove("semantic"), "\"semantic\"");
            yield return Case("bounds as text", d => d["bounds"] = "wide", "bounds");
            yield return Case("objects as an object", d => d["objects"] = new JObject(), "objects");
            yield return Case("a null where a list belongs", d => d["frames"] = JValue.CreateNull(), "frames");
        }

        private static TestCaseData Case(string name, Action<JObject> mutate, string expectedInError)
        {
            return new TestCaseData(mutate, expectedInError).SetName("Rejects_" + name.Replace(' ', '_'));
        }

        [TestCaseSource(nameof(Mutations))]
        public void TheSchema_RejectsABrokenDocument(Action<JObject> mutate, string expectedInError)
        {
            var document = ExtrasWriter.Write(Models.Full());
            Assert.IsEmpty(Validate(document), "the document is valid before it is broken");

            mutate(document);

            var errors = Validate(document);
            Assert.IsNotEmpty(errors, "the broken document passed");
            Assert.IsTrue(errors.Any(error => error.Contains(expectedInError)), "expected an error about " + expectedInError + " but got: " + string.Join("; ", errors));
        }

        [Test]
        public void TheSchema_AllowsKeysItDoesNotKnow_SoOtherToolsAndNewerVersionsCanAddThem()
        {
            var document = ExtrasWriter.Write(Models.Full());
            document["future"] = new JObject { ["a"] = 1 };
            document["objects"][0]["note"] = "hi";
            document["space"]["site"] = "x";

            Assert.IsEmpty(Validate(document));
        }
    }
}
