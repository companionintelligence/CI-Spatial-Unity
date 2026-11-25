// Tests for ExtrasMerge: a new export merged into an earlier document by object id, so a re-export lists each object
// once (the scanner's MergeExtras appended, and listed every pin twice).

using System;
using System.Linq;
using Companion.Spatial.Extras;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Companion.Spatial.Tests
{
    public class ExtrasMergeTests
    {
        private const double Tolerance = 1e-9;

        private static ScanExtras Doc(string space = "room-1", string created = null, string updated = null)
        {
            var extras = new ScanExtras();
            extras.Space.Id = space;
            extras.Space.CreatedAtUtc = created;
            extras.Space.UpdatedAtUtc = updated;
            return extras;
        }

        private static string[] Ids(System.Collections.Generic.IEnumerable<ScanObject> objects)
        {
            return objects.Select(o => o.Id).ToArray();
        }

        private static ScanObject At(ScanExtras extras, string id)
        {
            return extras.Objects.Single(o => o.Id == id);
        }

        // ---- by object id ----

        [Test]
        public void ObjectsWithTheSameId_AreReplaced_TheOthersKept_AndNewOnesAppended()
        {
            var existing = Doc();
            existing.Objects.Add(Models.CvObject("a", 1, "cup", 0, 0, 0));
            existing.Objects.Add(Models.CvObject("b", 2, "cup", 1, 0, 0));
            existing.Objects.Add(Models.CvObject("c", 3, "cup", 2, 0, 0));
            var incoming = Doc();
            incoming.Objects.Add(Models.CvObject("b", 2, "mug", 10, 0, 0));
            incoming.Objects.Add(Models.CvObject("d", 4, "cup", 3, 0, 0));

            var merged = ExtrasMerge.Merge(existing, incoming).Merged;

            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, Ids(merged.Objects), "an object keeps its place when it is replaced");
            Assert.AreEqual(0.0, At(merged, "a").Pose.Position.X);
            Assert.AreEqual(10.0, At(merged, "b").Pose.Position.X, "the new b replaced the old one");
            Assert.AreEqual("mug", At(merged, "b").Label.Tag);
            Assert.AreEqual(2.0, At(merged, "c").Pose.Position.X, "an object only the old file has is kept");
            Assert.AreEqual(3.0, At(merged, "d").Pose.Position.X);
        }

        [Test]
        public void ExportingTwice_ListsEachObjectAndFrameOnce()
        {
            ScanExtras Export(double shift)
            {
                var export = Doc(updated: "2026-11-25T10:00:00Z");
                export.Frames.Add(Models.Frame("snap_0001", 0, 1.5, 0));
                export.Frames.Add(Models.Frame("snap_0002", 1, 1.5, 0));
                export.Objects.Add(Models.CvObject("s-track-1", 1, "chair", 1 + shift, 0.5, 2, 0.6, "snap_0001"));
                export.Objects.Add(Models.DeviceObject("u1", "TABLE", 0, 0.75, 4));
                return export;
            }

            var first = ExtrasMerge.Merge(null, Export(0)).Merged;
            var second = ExtrasMerge.Merge(first, Export(0.1)).Merged;
            var third = ExtrasMerge.Merge(second, Export(0.2)).Merged;

            Assert.AreEqual(2, first.Objects.Count);
            Assert.AreEqual(2, second.Objects.Count, "v1's MergeExtras listed every pin twice here");
            Assert.AreEqual(2, third.Objects.Count);
            Assert.AreEqual(2, third.Frames.Count);
            Assert.AreEqual(1.2, At(third, "s-track-1").Pose.Position.X, Tolerance, "the latest export wins");
        }

        [Test]
        public void FramesAreMergedByIdLikewise()
        {
            var existing = Doc();
            existing.Frames.Add(Models.Frame("snap_0001", 0, 0, 0));
            existing.Frames.Add(Models.Frame("snap_0002", 1, 0, 0));
            var incoming = Doc();
            incoming.Frames.Add(Models.Frame("snap_0002", 9, 0, 0));
            incoming.Frames.Add(Models.Frame("snap_0003", 3, 0, 0));

            var merged = ExtrasMerge.Merge(existing, incoming).Merged;

            CollectionAssert.AreEqual(new[] { "snap_0001", "snap_0002", "snap_0003" }, merged.Frames.Select(f => f.Id).ToArray());
            Assert.AreEqual(9.0, merged.Frames[1].Pose.Position.X);
        }

        [Test]
        public void TheSameIdTwiceInOneDocument_IsOneEntry_TheLaterOneWins()
        {
            var incoming = Doc();
            incoming.Objects.Add(Models.CvObject("a", 1, "cup", 1, 0, 0));
            incoming.Objects.Add(Models.CvObject("a", 1, "cup", 2, 0, 0));

            var merged = ExtrasMerge.Merge(null, incoming).Merged;

            Assert.AreEqual(1, merged.Objects.Count);
            Assert.AreEqual(2.0, merged.Objects[0].Pose.Position.X);
        }

        [Test]
        public void ObjectsWithoutAnId_CannotBeMatched_SoTheyAreAllKept_WithAWarning()
        {
            var existing = Doc();
            existing.Objects.Add(Models.CvObject("", 0, "cup", 1, 0, 0));
            var incoming = Doc();
            incoming.Objects.Add(Models.CvObject("", 0, "cup", 2, 0, 0));

            var result = ExtrasMerge.Merge(existing, incoming);

            Assert.AreEqual(2, result.Merged.Objects.Count);
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("no id")), string.Join(" | ", result.Warnings));
        }

        // ---- the scene ----

        [Test]
        public void ANewSetOfPlanes_ReplacesTheOldOnes_ButNoPlanesKeepsThem()
        {
            var existing = Doc();
            existing.Planes.Add(Models.Wall("old-1"));
            existing.Planes.Add(Models.Wall("old-2"));
            var withPlanes = Doc();
            withPlanes.Planes.Add(Models.Wall("new-1"));
            var withoutPlanes = Doc();

            CollectionAssert.AreEqual(new[] { "new-1" }, ExtrasMerge.Merge(existing, withPlanes).Merged.Planes.Select(p => p.Id).ToArray(),
                "Space Setup gave a new scene: the old surfaces may be gone");
            CollectionAssert.AreEqual(new[] { "old-1", "old-2" }, ExtrasMerge.Merge(existing, withoutPlanes).Merged.Planes.Select(p => p.Id).ToArray(),
                "an export that read no scene does not wipe the one the file has");
        }

        // ---- time and bounds ----

        [Test]
        public void TheCreatedTimeIsTheEarliest_TheUpdatedTimeTheLatest()
        {
            var existing = Doc(created: "2026-11-24T08:00:00.0000000Z", updated: "2026-11-24T09:00:00.0000000Z");
            var incoming = Doc(created: "2026-11-25T10:00:00.0000000Z", updated: "2026-11-25T10:00:00.0000000Z");

            var merged = ExtrasMerge.Merge(existing, incoming).Merged;

            Assert.AreEqual("2026-11-24T08:00:00.0000000Z", merged.Space.CreatedAtUtc);
            Assert.AreEqual("2026-11-25T10:00:00.0000000Z", merged.Space.UpdatedAtUtc);
        }

        [Test]
        public void TimesAreComparedAsInstants_NotAsText()
        {
            // 10:00 at +02:00 is 08:00 UTC, earlier than 09:00 UTC, though its text sorts later
            var existing = Doc(created: "2026-11-25T10:00:00+02:00", updated: "2026-11-25T10:00:00+02:00");
            var incoming = Doc(created: "2026-11-25T09:00:00Z", updated: "2026-11-25T09:00:00Z");

            var merged = ExtrasMerge.Merge(existing, incoming).Merged;

            Assert.AreEqual("2026-11-25T10:00:00+02:00", merged.Space.CreatedAtUtc, "08:00 UTC is the earliest");
            Assert.AreEqual("2026-11-25T09:00:00Z", merged.Space.UpdatedAtUtc, "09:00 UTC is the latest");
        }

        [Test]
        public void AnEarlierFileWithNoCreatedTime_GivesItsLastExportAsTheEarliestTimeKnown()
        {
            var existing = Doc(updated: "2026-11-24T09:00:00.0000000Z");
            var incoming = Doc(created: "2026-11-25T10:00:00.0000000Z", updated: "2026-11-25T10:00:00.0000000Z");

            var merged = ExtrasMerge.Merge(existing, incoming).Merged;

            Assert.AreEqual("2026-11-24T09:00:00.0000000Z", merged.Space.CreatedAtUtc);
        }

        [Test]
        public void ANewExportWithNoTime_KeepsTheTimesOfTheFile_ItNeverInventsOne()
        {
            var existing = Doc(created: "2026-11-24T08:00:00.0000000Z", updated: "2026-11-24T09:00:00.0000000Z");

            var merged = ExtrasMerge.Merge(existing, Doc()).Merged;

            Assert.AreEqual("2026-11-24T08:00:00.0000000Z", merged.Space.CreatedAtUtc);
            Assert.AreEqual("2026-11-24T09:00:00.0000000Z", merged.Space.UpdatedAtUtc);
        }

        [Test]
        public void TheBounds_AreRecomputedFromWhatTheMergedFileHolds_NotKeptFromTheOldFile()
        {
            var existing = Doc();
            existing.Objects.Add(Models.CvObject("a", 1, "cup", 0, 0, 0));
            existing.Objects.Add(Models.CvObject("b", 2, "cup", 10, 0, 0));
            existing.Bounds = new ScanBounds(new Vec3(5, 0, 0), new Vec3(10, 0, 0));
            var incoming = Doc();
            incoming.Objects.Add(Models.CvObject("b", 2, "cup", 2, 0, 0));

            var merged = ExtrasMerge.Merge(existing, incoming).Merged;

            Assert.AreEqual(1.0, merged.Bounds.Center.X, Tolerance, "b moved from x = 10 to x = 2: the box is 0 to 2");
            Assert.AreEqual(2.0, merged.Bounds.Size.X, Tolerance);
        }

        [Test]
        public void TheBounds_AlsoCoverTheBoundsOfTheNewExport_WhichMayKnowMoreThanTheObjects()
        {
            var existing = Doc();
            existing.Objects.Add(Models.CvObject("a", 1, "cup", 0, 0, 0));
            var incoming = Doc();
            incoming.Objects.Add(Models.CvObject("b", 2, "cup", 2, 0, 0));
            incoming.Bounds = new ScanBounds(new Vec3(2, 0, 0), new Vec3(8, 0, 0)); // the room outline reaches x = -2 to 6

            var merged = ExtrasMerge.Merge(existing, incoming).Merged;

            Assert.AreEqual(-2.0, merged.Bounds.Min.X, Tolerance);
            Assert.AreEqual(6.0, merged.Bounds.Max.X, Tolerance);
        }

        [Test]
        public void ADocumentWithNothingInIt_HasNoBounds()
        {
            Assert.IsNull(ExtrasMerge.Merge(Doc(), Doc()).Merged.Bounds);
        }

        // ---- the first export, and an earlier file that cannot be used ----

        [Test]
        public void WithNoEarlierDocument_TheResultHasTheContentOfTheNewOne_AndNoWarning()
        {
            var incoming = Models.Full();

            var result = ExtrasMerge.Merge(null, incoming);

            Assert.IsEmpty(result.Warnings);
            Assert.IsFalse(result.ExistingDiscarded);
            Assert.AreNotSame(incoming, result.Merged, "the result is a document of its own");
            JsonAssert.AreSame(ExtrasWriter.Write(incoming), ExtrasWriter.Write(result.Merged), "the same content");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   \r\n ")]
        public void NoTextOfAnEarlierDocument_IsNotAProblem(string existing)
        {
            var result = ExtrasMerge.MergeFromText(existing, Models.Full());

            Assert.IsEmpty(result.Warnings);
            Assert.IsFalse(result.ExistingDiscarded);
            Assert.AreEqual(2, result.Merged.Objects.Count);
        }

        // v1 made every later export of the space throw when the earlier glTF's extras were cut short.
        [TestCase("{ \"asset\": ")]
        [TestCase("{")]
        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("5")]
        [TestCase("{}")]
        [TestCase("{ \"unrelated\": true }")]
        public void ACorruptEarlierDocument_IsReplaced_WithAWarning_NotAnException(string existing)
        {
            ExtrasMergeResult result = null;
            Assert.DoesNotThrow(() => result = ExtrasMerge.MergeFromText(existing, Models.Full()));

            Assert.IsTrue(result.ExistingDiscarded);
            Assert.AreEqual(1, result.Warnings.Count, string.Join(" | ", result.Warnings));
            StringAssert.Contains("replaced", result.Warnings[0]);
            Assert.AreEqual(2, result.Merged.Objects.Count, "the new export stands alone");
            Assert.AreEqual("room-1", result.Merged.Space.Id);
        }

        [Test]
        public void ACorruptEarlierObject_IsReplacedToo()
        {
            var result = ExtrasMerge.MergeFromObject(new JObject { ["nothing"] = "useful" }, Models.Full());

            Assert.IsTrue(result.ExistingDiscarded);
            Assert.AreEqual(1, result.Warnings.Count);
            Assert.IsFalse(ExtrasMerge.MergeFromObject(null, Models.Full()).ExistingDiscarded);
        }

        [Test]
        public void AnEarlierDocumentOfAnotherSpace_IsReplaced_WithAWarning()
        {
            var existing = Doc("room-1");
            existing.Objects.Add(Models.CvObject("a", 1, "cup", 0, 0, 0));
            var incoming = Doc("room-2");
            incoming.Objects.Add(Models.CvObject("b", 2, "cup", 1, 0, 0));

            var result = ExtrasMerge.Merge(existing, incoming);

            Assert.IsTrue(result.ExistingDiscarded);
            StringAssert.Contains("room-1", result.Warnings.Single());
            StringAssert.Contains("room-2", result.Warnings.Single());
            CollectionAssert.AreEqual(new[] { "b" }, Ids(result.Merged.Objects));
            Assert.AreEqual("room-2", result.Merged.Space.Id);
        }

        [Test]
        public void ASpaceWithoutAnIdOnEitherSide_DoesNotStopTheMerge()
        {
            var existing = Doc("");
            existing.Objects.Add(Models.CvObject("a", 1, "cup", 0, 0, 0));
            var incoming = Doc("room-2");

            var result = ExtrasMerge.Merge(existing, incoming);

            Assert.IsFalse(result.ExistingDiscarded);
            Assert.AreEqual(1, result.Merged.Objects.Count);
            Assert.AreEqual("room-2", result.Merged.Space.Id);
        }

        // ---- a v1 file as the earlier document ----

        [Test]
        public void AnEarlierV1File_IsUpgraded_AndItsPinsAreNotListedTwice()
        {
            var v1 = Models.ExporterShapedV1();
            var sameContent = ExtrasReader.Read(v1).Extras; // what the scanner would export again for the same pins

            var result = ExtrasMerge.MergeFromObject(v1, sameContent);

            Assert.IsFalse(result.ExistingDiscarded);
            Assert.AreEqual(2, result.Merged.Objects.Count, "v1's MergeExtras listed each of the two pins twice");
            Assert.AreEqual(1, result.Merged.Frames.Count);
            Assert.AreEqual(1, result.Merged.Planes.Count);
            Assert.AreEqual("room-1", result.Merged.Space.Id);
        }

        [Test]
        public void AnEarlierV1File_KeepsItsPinsNextToTheNewOnes()
        {
            var v1 = Models.ExporterShapedV1();
            var incoming = Doc("room-1", updated: "2026-11-25T11:00:00.0000000Z");
            incoming.Objects.Add(Models.CvObject("run2-track-1", 1, "lamp", 5, 0, 5));

            var merged = ExtrasMerge.MergeFromObject(v1, incoming).Merged;

            CollectionAssert.AreEqual(new[] { "session-1-track-7", "anchor-anchor-1", "run2-track-1" }, Ids(merged.Objects));
            Assert.AreEqual("2026-11-25T10:15:30.1234567Z", merged.Space.CreatedAtUtc, "the v1 file's own export time is the earliest known");
            Assert.AreEqual("2026-11-25T11:00:00.0000000Z", merged.Space.UpdatedAtUtc);
        }

        [Test]
        public void TheWarningsOfReadingTheEarlierDocument_AreHanded_OnWithTheMergeWarnings()
        {
            var json = Models.ExporterShapedV1();
            var pins = (JArray)json["cv_mapping"]["pins"];
            pins.Add(pins[0].DeepClone());

            var result = ExtrasMerge.MergeFromObject(json, Doc("room-1"));

            Assert.IsTrue(result.Warnings.Any(w => w.Contains("listed more than once")), string.Join(" | ", result.Warnings));
            Assert.IsTrue(result.Warnings.All(w => w.StartsWith("earlier document: ")), string.Join(" | ", result.Warnings));
        }

        // ---- the rest of the document ----

        [Test]
        public void Consent_IsTheNewExports_AndIsNeverCarriedOverFromTheOldFile()
        {
            var existing = Doc();
            existing.Consent = new ScanConsent { NoticeVersion = "old", AgreedAtUtc = "2026-11-24T08:00:00Z" };
            var consenting = Doc();
            consenting.Consent = new ScanConsent { NoticeVersion = "new", AgreedAtUtc = "2026-11-25T08:00:00Z", Server = "https://s.example.test" };

            Assert.AreEqual("new", ExtrasMerge.Merge(existing, consenting).Merged.Consent.NoticeVersion);

            var result = ExtrasMerge.Merge(existing, Doc());
            Assert.IsNull(result.Merged.Consent, "a record of consent for the old data is not consent for the new");
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("consent")), string.Join(" | ", result.Warnings));
        }

        [Test]
        public void ANewExportWithNoConsent_AndNoEarlierConsent_HasNoWarning()
        {
            var result = ExtrasMerge.Merge(Doc(), Doc());

            Assert.IsNull(result.Merged.Consent);
            Assert.IsEmpty(result.Warnings);
        }

        [Test]
        public void TheDevice_IsTheNewExportsWhenItHasOne_ElseTheEarlierFiles()
        {
            var existing = Doc();
            existing.Device = new ScanDevice { Model = "Quest 3" };
            var other = Doc();
            other.Device = new ScanDevice { Model = "Quest Pro" };

            Assert.AreEqual("Quest Pro", ExtrasMerge.Merge(existing, other).Merged.Device.Model);
            Assert.AreEqual("Quest 3", ExtrasMerge.Merge(existing, Doc()).Merged.Device.Model);
        }

        [Test]
        public void UnknownKeys_OfBothDocuments_AreKept_TheNewOnesWinningAClash()
        {
            var existing = Doc();
            existing.Unknown["a"] = 1;
            existing.Unknown["b"] = 2;
            existing.Space.Unknown["site"] = "old";
            var incoming = Doc();
            incoming.Unknown["b"] = 3;
            incoming.Unknown["c"] = 4;
            incoming.Space.Unknown["floor"] = 2;

            var merged = ExtrasMerge.Merge(existing, incoming).Merged;

            Assert.AreEqual(1, (int)merged.Unknown["a"]);
            Assert.AreEqual(3, (int)merged.Unknown["b"]);
            Assert.AreEqual(4, (int)merged.Unknown["c"]);
            Assert.AreEqual("old", (string)merged.Space.Unknown["site"]);
            Assert.AreEqual(2, (int)merged.Space.Unknown["floor"]);
        }

        // Positions in two coordinate frames cannot share one file.
        [Test]
        public void AnEarlierDocumentInAnotherCoordinateFrame_IsReplaced_WithAWarning()
        {
            var existing = Doc();
            existing.Space.Frame = "unity-world";
            existing.Objects.Add(Models.CvObject("a", 1, "cup", 0, 0, 0));
            var incoming = Doc();
            incoming.Space.Frame = "local-floor";
            incoming.Objects.Add(Models.CvObject("b", 2, "cup", 1, 0, 0));

            var result = ExtrasMerge.Merge(existing, incoming);

            Assert.IsTrue(result.ExistingDiscarded);
            StringAssert.Contains("unity-world", result.Warnings.Single());
            StringAssert.Contains("local-floor", result.Warnings.Single());
            CollectionAssert.AreEqual(new[] { "b" }, Ids(result.Merged.Objects));
            Assert.AreEqual("local-floor", result.Merged.Space.Frame);
        }

        // ---- the inputs are left alone ----

        [Test]
        public void MergingDoesNotChangeEitherInput()
        {
            var existing = Models.Full();
            existing.Objects.Add(Models.CvObject("extra", 9, "bottle", 3, 0, 3));
            var incoming = Doc("room-1", updated: "2026-11-26T10:00:00.0000000Z");
            incoming.Objects.Add(Models.CvObject("s1-track-1", 1, "chair", 7, 7, 7));
            incoming.Planes.Add(Models.Wall("w9"));
            string existingBefore = ExtrasWriter.ToJson(existing);
            string incomingBefore = ExtrasWriter.ToJson(incoming);

            ExtrasMerge.Merge(existing, incoming);

            Assert.AreEqual(existingBefore, ExtrasWriter.ToJson(existing));
            Assert.AreEqual(incomingBefore, ExtrasWriter.ToJson(incoming));
        }

        [Test]
        public void ANewExport_IsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => ExtrasMerge.Merge(Doc(), null));
            Assert.Throws<ArgumentNullException>(() => ExtrasMerge.MergeFromText("{}", null));
            Assert.Throws<ArgumentNullException>(() => ExtrasMerge.MergeFromObject(new JObject(), null));
        }

        [Test]
        public void TheMergeIsDeterministic()
        {
            var existing = Models.Full();
            var incoming = Doc("room-1", updated: "2026-11-26T10:00:00.0000000Z");
            incoming.Objects.Add(Models.CvObject("n1", 1, "lamp", 1, 1, 1));

            Assert.AreEqual(ExtrasWriter.ToJson(ExtrasMerge.Merge(existing, incoming).Merged), ExtrasWriter.ToJson(ExtrasMerge.Merge(existing, incoming).Merged));
        }
    }
}
