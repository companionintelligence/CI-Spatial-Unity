// Round trips: a v2 document read and written again is the same document; a v1 file upgraded to v2 and read back keeps
// its content and everything v2 had no place for.

using System.Linq;
using Companion.Spatial.Extras;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Companion.Spatial.Tests
{
    public class ExtrasRoundTripTests
    {
        private static ScanExtras Read(JToken json)
        {
            var result = ExtrasReader.Read(json.ToString());
            Assert.IsTrue(result.Success, result.Error);
            return result.Extras;
        }

        [Test]
        public void AV2Document_WrittenAfterReading_IsTheSameDocument()
        {
            var original = JsonAssert.ParseObject(Models.V2Document);

            var again = ExtrasWriter.Write(Read(original));

            JsonAssert.AreSame(original, again, "reading and writing a v2 document");
        }

        [Test]
        public void AModelsDocument_ReadAndWrittenAgain_IsStable()
        {
            var first = ExtrasWriter.Write(Models.Full());

            var second = ExtrasWriter.Write(Read(first));

            JsonAssert.AreSame(first, second, "writing, reading and writing again");
            Assert.AreEqual(first.ToString(), second.ToString(), "and the same text, key by key");
        }

        [Test]
        public void V1ToV2ToV1CompatibleRead_KeepsTheContentOfTheGoldenFile()
        {
            var upgraded = Read(Models.GoldenV1());
            var v2 = ExtrasWriter.Write(upgraded);

            var result = ExtrasReader.Read(v2);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2, result.SourceVersion, "what the scanner writes now says it is v2");
            Assert.IsEmpty(result.Warnings);
            var back = result.Extras;

            // the same document again, so nothing was lost or made up on the way
            JsonAssert.AreSame(v2, ExtrasWriter.Write(back), "the v2 form of the golden file, read and written again");

            // and what the golden file said is still said
            Assert.AreEqual("room-1", back.Space.Id);
            Assert.AreEqual(2, back.Objects.Count);
            Assert.AreEqual("chair", back.Objects[0].Label.Text);
            Assert.AreEqual("chair", back.Objects[0].Label.Tag);
            Assert.AreEqual(0.5, back.Objects[0].Confidence, 1e-12);
            Assert.AreEqual(1.0, back.Objects[0].Pose.Position.X, 1e-12);
            Assert.AreEqual(0.5, back.Objects[0].Pose.Position.Y, 1e-12);
            Assert.AreEqual(2.0, back.Objects[0].Pose.Position.Z, 1e-12);
            Assert.AreEqual("TABLE", back.Objects[1].Label.Semantic);
            Assert.AreEqual("snap_0001", back.Frames[0].Id);
            Assert.AreEqual(12.0, back.Frames[0].Intrinsics.Fy, 1e-12);
            Assert.AreEqual(1.0, back.Bounds.Size.X, 1e-12);
        }

        [Test]
        public void WhatV2HasNoPlaceFor_SurvivesTheRoundTripUnderV1()
        {
            var upgraded = Read(Models.ExporterShapedV1());

            var back = Read(ExtrasWriter.Write(upgraded));

            Assert.IsTrue(JToken.DeepEquals(upgraded.Unknown, back.Unknown), "the root");
            Assert.IsTrue(JToken.DeepEquals(upgraded.Objects[0].Unknown, back.Objects[0].Unknown), "the chair");
            Assert.AreEqual("Physics", (string)back.Objects[0].Unknown["v1"]["hit_type"]);
            Assert.IsTrue(JToken.DeepEquals(upgraded.Planes[0].Unknown, back.Planes[0].Unknown), "the plane");
            Assert.IsTrue(JToken.DeepEquals(upgraded.Frames[0].Unknown, back.Frames[0].Unknown), "the frame");
            Assert.AreEqual(100, (int)back.Planes[0].Unknown["v1"]["mesh_vertex_count"]);
        }

        [Test]
        public void TheV1FileWithItsTopLevelCopies_UpgradesToTheSameDocumentAsTheGoldenFile_PlusTheScene()
        {
            var fromGolden = ExtrasWriter.Write(Read(Models.GoldenV1()));
            var fromExporter = ExtrasWriter.Write(Read(Models.ExporterShapedV1()));

            // the exporter-shaped file only adds a time, the scene (as a plane and notes) and the repeated members
            Assert.IsTrue(JToken.DeepEquals(fromGolden["objects"], fromExporter["objects"]), "the objects");
            Assert.IsTrue(JToken.DeepEquals(fromGolden["frames"], fromExporter["frames"]), "the frames");
            Assert.IsTrue(JToken.DeepEquals(fromGolden["bounds"], fromExporter["bounds"]), "the bounds");
            Assert.AreEqual(0, fromGolden["planes"].Count());
            Assert.AreEqual(1, fromExporter["planes"].Count());
        }

        [Test]
        public void AnUpgradedFile_CanBeMergedAndWrittenAgainWithoutLosingAnything()
        {
            var upgraded = Read(Models.ExporterShapedV1());

            var merged = ExtrasMerge.Merge(upgraded, Read(Models.ExporterShapedV1())).Merged;

            JsonAssert.AreSame(ExtrasWriter.Write(upgraded)["objects"], ExtrasWriter.Write(merged)["objects"], "the objects after merging a file with itself");
            Assert.IsTrue(JToken.DeepEquals(upgraded.Unknown, merged.Unknown));
        }

        [Test]
        public void TheDocumentsOfTheTests_AllReadBack()
        {
            foreach (var pair in Models.ValidModels())
            {
                var written = ExtrasWriter.Write(pair.Value);
                var back = ExtrasReader.Read(written);

                Assert.IsTrue(back.Success, pair.Key + ": " + back.Error);
                Assert.IsEmpty(back.Warnings, pair.Key + ": " + string.Join(" | ", back.Warnings));
                JsonAssert.AreSame(written, ExtrasWriter.Write(back.Extras), pair.Key);
            }
        }
    }
}
