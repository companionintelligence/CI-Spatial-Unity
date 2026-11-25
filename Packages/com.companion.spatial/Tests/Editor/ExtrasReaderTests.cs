// Tests for ExtrasReader: the golden v1 file and the v1 files the exporter really writes load into the v2 model, v2
// files load as they are, unknown keys are kept, and a bad document gives an error instead of an exception.

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Companion.Spatial.Extras;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Companion.Spatial.Tests
{
    public class ExtrasReaderTests
    {
        private const double Tolerance = 1e-12;

        private static ScanExtras ReadOk(string json)
        {
            var result = ExtrasReader.Read(json);
            Assert.IsTrue(result.Success, "the read failed: " + result.Error);
            return result.Extras;
        }

        private static ExtrasReadResult ReadOkResult(JObject json)
        {
            var result = ExtrasReader.Read(json);
            Assert.IsTrue(result.Success, "the read failed: " + result.Error);
            return result;
        }

        private static void AssertVec(Vec3 expected, Vec3 actual, string what = "")
        {
            Assert.AreEqual(expected.X, actual.X, Tolerance, what + " x of " + actual);
            Assert.AreEqual(expected.Y, actual.Y, Tolerance, what + " y of " + actual);
            Assert.AreEqual(expected.Z, actual.Z, Tolerance, what + " z of " + actual);
        }

        private static void AssertKeys(JObject actual, params string[] expected)
        {
            CollectionAssert.AreEquivalent(expected, actual.Properties().Select(property => property.Name).ToArray());
        }

        // ---- the golden v1 file ----

        [Test]
        public void GoldenV1_LoadsAsAVersionOneFile_WithNoErrorAndNoWarning()
        {
            var result = ExtrasReader.Read(TestPaths.ReadGoldenV1());

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNull(result.Error);
            Assert.AreEqual(1, result.SourceVersion);
            Assert.IsNotNull(result.Extras);
            Assert.IsEmpty(result.Warnings);
        }

        [Test]
        public void GoldenV1_GivesTheSpaceItsIdItsTimeAndTheUnityFrame()
        {
            var extras = ReadOk(TestPaths.ReadGoldenV1());

            Assert.AreEqual("room-1", extras.Space.Id);
            Assert.AreEqual("<timestamp_utc>", extras.Space.UpdatedAtUtc, "v1's timestamp_utc, as written");
            Assert.IsNull(extras.Space.CreatedAtUtc, "v1 did not know when the file was first made");
            Assert.AreEqual("unity-world", extras.Space.Frame);
            Assert.IsNull(extras.Consent);
            Assert.IsNull(extras.Device);
        }

        [Test]
        public void GoldenV1_TheChairPin_BecomesACvObjectWithItsLabelPositionAndScore()
        {
            var extras = ReadOk(TestPaths.ReadGoldenV1());

            Assert.AreEqual(2, extras.Objects.Count);
            var chair = extras.Objects[0];
            Assert.AreEqual("session-1-track-7", chair.Id, "the session and the track number, so numbers of other runs do not collide");
            Assert.AreEqual(7, chair.TrackId);
            Assert.AreEqual("chair", chair.Label.Text);
            Assert.AreEqual("chair", chair.Label.Tag);
            Assert.AreEqual("OTHER", chair.Label.Semantic);
            Assert.AreEqual(0.5, chair.Confidence, Tolerance);
            Assert.AreEqual(ObjectSource.Cv, chair.Source);
            AssertVec(new Vec3(1.0, 0.5, 2.0), chair.Pose.Position, "position");
            Assert.AreEqual(new Quat(0.125, 0.25, 0.375, 0.5), chair.Pose.Rotation);
            Assert.AreEqual(new Vec3(0, 1, 0), chair.Normal.Value);
            Assert.IsNull(chair.Bounds, "a CV pin has no extent");
            Assert.IsTrue(string.IsNullOrEmpty(chair.AnchorUuid));

            var seen = chair.Observations.Single();
            Assert.AreEqual("snap_0001", seen.FrameId);
            Assert.AreEqual(0.5, seen.Score, Tolerance);
            Assert.IsTrue(seen.Box.HasValue);
            // bbox_normalized [x, y, width, height] from the top left
            Assert.AreEqual(0.0625, seen.Box.Value.X1, Tolerance);
            Assert.AreEqual(0.125, seen.Box.Value.Y1, Tolerance);
            Assert.AreEqual(0.3125, seen.Box.Value.X2, Tolerance);
            Assert.AreEqual(0.625, seen.Box.Value.Y2, Tolerance);
        }

        [Test]
        public void GoldenV1_TheTablePin_BecomesADeviceObjectWithItsExtent()
        {
            var extras = ReadOk(TestPaths.ReadGoldenV1());

            var table = extras.Objects[1];
            Assert.AreEqual("anchor-anchor-1", table.Id);
            Assert.AreEqual(0, table.TrackId);
            Assert.AreEqual("TABLE", table.Label.Text);
            Assert.AreEqual("table", table.Label.Tag);
            Assert.AreEqual("TABLE", table.Label.Semantic);
            Assert.AreEqual(1.0, table.Confidence, Tolerance);
            Assert.AreEqual(ObjectSource.Device, table.Source);
            AssertVec(new Vec3(0, 1.5, 4), table.Pose.Position);
            Assert.AreEqual(Quat.Identity, table.Pose.Rotation);
            Assert.AreEqual("anchor-1", table.AnchorUuid);
            Assert.IsEmpty(table.Observations, "a headset anchor was not seen in a camera frame");
            Assert.IsNotNull(table.Bounds);
            AssertVec(new Vec3(0, 1.5, 4), table.Bounds.Center, "the volume is centred on the anchor");
            AssertVec(new Vec3(1.5, 0.75, 0.5), table.Bounds.Size, "volume_size");
        }

        [Test]
        public void GoldenV1_TheSnapshot_BecomesAFrameWithItsCameraPoseAndIntrinsics()
        {
            var extras = ReadOk(TestPaths.ReadGoldenV1());

            var frame = extras.Frames.Single();
            Assert.AreEqual("snap_0001", frame.Id);
            Assert.AreEqual("snapshots/snap_0001.jpg", frame.Image);
            Assert.IsNull(frame.TimestampUtc, "v1 only had the scan clock");
            AssertVec(new Vec3(0.5, 1.5, -1.0), frame.Pose.Position);
            Assert.AreEqual(new Quat(0.125, 0.25, 0.375, 0.5), frame.Pose.Rotation);

            // the 4x4 matrix [1, 2, 3, 4, 11, 12, 13, 14, ...]: fx = m00, fy = m11, cx = m02, cy = m12
            Assert.AreEqual(1.0, frame.Intrinsics.Fx, Tolerance);
            Assert.AreEqual(12.0, frame.Intrinsics.Fy, Tolerance);
            Assert.AreEqual(3.0, frame.Intrinsics.Cx, Tolerance);
            Assert.AreEqual(13.0, frame.Intrinsics.Cy, Tolerance);
            Assert.AreEqual(640, frame.Intrinsics.Width);
            Assert.AreEqual(480, frame.Intrinsics.Height);
        }

        [Test]
        public void GoldenV1_HasTheBoundsAndNoPlanes()
        {
            var extras = ReadOk(TestPaths.ReadGoldenV1());

            AssertVec(new Vec3(0.5, 1.0, 3.0), extras.Bounds.Center);
            AssertVec(new Vec3(1.0, 1.0, 2.0), extras.Bounds.Size);
            Assert.IsEmpty(extras.Planes, "the scene said \"available\": false");
        }

        // What v2 has no place for is kept, not dropped: under "v1" of the part it belonged to.
        [Test]
        public void GoldenV1_KeepsTheFieldsV2HasNoPlaceFor_UnderV1()
        {
            var extras = ReadOk(TestPaths.ReadGoldenV1());

            var chair = (JObject)extras.Objects[0].Unknown["v1"];
            AssertKeys(chair, "bbox_pixels", "plane_size", "prompt", "timestamp", "capture_session_id", "capture_frame_index", "placement_confidence", "hit_type");
            Assert.AreEqual("Physics", (string)chair["hit_type"]);
            Assert.AreEqual("chair . table .", (string)chair["prompt"]);
            Assert.AreEqual("session-1", (string)chair["capture_session_id"]);
            Assert.AreEqual(3, (int)chair["capture_frame_index"]);
            Assert.AreEqual(0.75, (double)chair["placement_confidence"], Tolerance);
            Assert.AreEqual(12.5, (double)chair["timestamp"], Tolerance);

            var table = (JObject)extras.Objects[1].Unknown["v1"];
            Assert.AreEqual("ArPlane", (string)table["hit_type"]);
            Assert.IsNull(table["prompt"], "a null is not kept");
            Assert.IsNull(table["capture_id"], "an empty string is not kept");
            Assert.AreEqual(1.5, (double)table["plane_size"][0], Tolerance);

            var frame = (JObject)extras.Frames[0].Unknown["v1"];
            AssertKeys(frame, "timestamp", "frustum");
            Assert.AreEqual(20.5, (double)frame["timestamp"], Tolerance);
            Assert.AreEqual(2.0, (double)frame["frustum"]["distance"], Tolerance);

            var root = (JObject)extras.Unknown["v1"];
            AssertKeys(root, "meta_scene");
            Assert.IsFalse((bool)root["meta_scene"]["available"]);
        }

        // ---- a v1 file as the exporter really writes it ----

        [Test]
        public void ExporterShapedV1_RepeatsFiveMembersAtTheTopLevel_ReadOnce()
        {
            var result = ReadOkResult(Models.ExporterShapedV1());
            var extras = result.Extras;

            Assert.AreEqual(1, result.SourceVersion);
            Assert.AreEqual("room-1", extras.Space.Id);
            Assert.AreEqual("2026-11-25T10:15:30.1234567Z", extras.Space.UpdatedAtUtc);
            Assert.AreEqual(2, extras.Objects.Count, "the top-level copies of pins are not read a second time");
            Assert.AreEqual(1, extras.Frames.Count);
            AssertVec(new Vec3(0.5, 1.0, 3.0), extras.Bounds.Center);
            Assert.IsEmpty(result.Warnings);
        }

        [Test]
        public void ExporterShapedV1_TheSceneAnchor_BecomesAPlane()
        {
            var extras = ReadOk(Models.ExporterShapedV1().ToString());

            var plane = extras.Planes.Single();
            Assert.AreEqual("anchor-1", plane.Id);
            Assert.AreEqual("table", plane.Label);
            Assert.AreEqual("TABLE", plane.Semantic);
            AssertVec(new Vec3(1, 0.5, 2), plane.Pose.Position);
            Assert.AreEqual(new Quat(0.125, 0.25, 0.375, 0.5), plane.Pose.Rotation);
            Assert.AreEqual(new Vec2(1, 0.5), plane.Center.Value);
            Assert.AreEqual(new Vec2(1.5, 0.75), plane.Size.Value);
            CollectionAssert.AreEqual(new[] { new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(1, 0, 1) }, plane.Boundary);

            var kept = (JObject)plane.Unknown["v1"];
            Assert.IsFalse((bool)kept["has_volume"]);
            Assert.IsTrue((bool)kept["has_mesh"]);
            Assert.AreEqual(100, (int)kept["mesh_vertex_count"]);
            Assert.AreEqual(50, (int)kept["mesh_triangle_count"]);
            Assert.IsNull(kept["plane_size"], "the plane's size is the plane's size, not a leftover");
        }

        [Test]
        public void ExporterShapedV1_KeepsTheSceneNotesAndTheBoundaryUnderV1()
        {
            var json = Models.ExporterShapedV1();
            json["boundary"] = JsonAssert.Parse("[ [0,0,0], [4,0,0], [4,0,3] ]");
            json["floorplan"] = JsonAssert.Parse("[ [0,0,0], [4,0,0], [4,0,3] ]");
            ((JObject)json["cv_mapping"])["boundary"] = json["boundary"].DeepClone();
            ((JObject)json["cv_mapping"])["floorplan"] = json["floorplan"].DeepClone();

            var extras = ReadOk(json.ToString());

            var kept = (JObject)extras.Unknown["v1"];
            AssertKeys(kept, "meta_scene", "boundary", "floorplan");
            Assert.AreEqual(3.5, (double)kept["meta_scene"]["timestamp"], Tolerance);
            Assert.AreEqual("room-1", (string)kept["meta_scene"]["primary_room_uuid"]);
            Assert.IsNull(kept["meta_scene"]["anchors"], "the anchors became planes");
            Assert.IsNull(kept["meta_scene"]["error"], "a null is not kept");
            Assert.AreEqual(3, ((JArray)kept["boundary"]).Count);
        }

        [Test]
        public void ANullListAndAMissingOne_GiveEmptyLists()
        {
            var json = JsonAssert.ParseObject(@"{
                'schema_version': '1.0', 'units': 'meters', 'pins': null,
                'cv_mapping': { 'pins': [] }, 'meta_scene': { 'available': false }
            }");

            var result = ReadOkResult(json);

            Assert.IsEmpty(result.Extras.Objects);
            Assert.IsEmpty(result.Extras.Frames);
            Assert.IsEmpty(result.Extras.Planes);
            Assert.IsNull(result.Extras.Bounds);
            Assert.AreEqual("default", result.Extras.Space.Id);
            Assert.IsEmpty(result.Warnings);
        }

        // Re-exporting with the old exporter listed every pin and snapshot twice.
        [Test]
        public void APinListedTwice_IsOneObject_WithAWarning()
        {
            var json = Models.ExporterShapedV1();
            foreach (var holder in new[] { json, (JObject)json["cv_mapping"] })
            {
                var pins = (JArray)holder["pins"];
                holder["pins"] = new JArray(pins.Concat(pins.Select(pin => pin.DeepClone())));
                var snapshots = (JArray)holder["snapshots"];
                holder["snapshots"] = new JArray(snapshots.Concat(snapshots.Select(snapshot => snapshot.DeepClone())));
            }

            var result = ReadOkResult(json);

            Assert.AreEqual(2, result.Extras.Objects.Count);
            Assert.AreEqual(1, result.Extras.Frames.Count);
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("listed more than once")), string.Join(" | ", result.Warnings));
        }

        [Test]
        public void KeysTheReaderDoesNotKnow_AreKeptAtTheRootAndInsideCvMapping()
        {
            var json = Models.GoldenV1();
            json["custom"] = "kept";
            json["tool"] = JsonAssert.Parse("{ 'name': 'viewer', 'edits': [ 1, 2 ] }");
            ((JObject)json["cv_mapping"])["note"] = "inside";

            var extras = ReadOk(json.ToString());

            Assert.AreEqual("kept", (string)extras.Unknown["custom"]);
            Assert.AreEqual("viewer", (string)extras.Unknown["tool"]["name"]);
            Assert.AreEqual(2, ((JArray)extras.Unknown["tool"]["edits"]).Count);
            Assert.AreEqual("inside", (string)extras.Unknown["v1"]["cv_mapping"]["note"]);
        }

        // A "device" or "objects" key that some other tool added to a v1 file would be read as that part by a v2 reader.
        [Test]
        public void V1RootKeysWithTheNameOfAV2Part_AreKeptUnderV1_NotAtTheRoot()
        {
            var json = Models.GoldenV1();
            json["consent"] = "x";
            json["device"] = JsonAssert.Parse("{ 'model': 'phone' }");
            json["objects"] = JsonAssert.Parse("[ 1, 2 ]");
            json["schema"] = "https://json-schema.org/draft/2020-12/schema";
            json["custom"] = "kept";

            var extras = ReadOk(json.ToString());

            Assert.IsNull(extras.Consent);
            Assert.IsNull(extras.Device);
            Assert.AreEqual(2, extras.Objects.Count, "the pins, not the foreign list");
            AssertKeys(extras.Unknown, "custom", "v1");
            Assert.AreEqual("x", (string)extras.Unknown["v1"]["consent"]);
            Assert.AreEqual("phone", (string)extras.Unknown["v1"]["device"]["model"]);
            Assert.AreEqual(2, ((JArray)extras.Unknown["v1"]["objects"]).Count);
            Assert.AreEqual("https://json-schema.org/draft/2020-12/schema", (string)extras.Unknown["v1"]["schema"]);

            var written = ExtrasWriter.Write(extras);
            Assert.IsNull(written["consent"]);
            Assert.IsNull(written["device"]);
            Assert.AreEqual(2, written["objects"].Count());
            var again = ExtrasReader.Read(written);
            Assert.IsTrue(again.Success, again.Error);
            Assert.IsEmpty(again.Warnings);
        }

        [TestCase("{ 'text': '', 'tag': 'couch' }", "", "couch")]
        [TestCase("{ 'tag': 'couch' }", "couch", "couch")]
        [TestCase("{ 'text': 'sofa', 'tag': '' }", "sofa", "")]
        [TestCase("{ 'text': 'sofa' }", "sofa", "couch")]
        [TestCase("{ }", "", "")]
        public void V2Label_KeepsWhatIsWrittenEvenWhenEmpty_AndFillsInOnlyWhatIsMissing(string label, string text, string tag)
        {
            var read = ReadOk("{ 'schema_version': 2, 'objects': [ { 'id': 'o', 'label': " + label + " } ] }").Objects[0].Label;

            Assert.AreEqual(text, read.Text);
            Assert.AreEqual(tag, read.Tag);
        }

        [Test]
        public void AStringThatWasWrittenAsANumber_IsReadAsItsText()
        {
            var result = ExtrasReader.Read("{ 'schema_version': 2, 'device': { 'app_version': 1.4, 'model': 7 }, 'frames': [ { 'id': 12 } ], 'objects': [ { 'id': 5 } ] }");

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("1.4", result.Extras.Device.AppVersion);
            Assert.AreEqual("7", result.Extras.Device.Model);
            Assert.AreEqual("12", result.Extras.Frames[0].Id);
            Assert.AreEqual("5", result.Extras.Objects[0].Id);
            Assert.IsEmpty(result.Warnings);
        }

        [Test]
        public void ANegativeTrackId_IsReadAsZero_WithAWarning_InV2AndInV1()
        {
            var v2 = ExtrasReader.Read("{ 'schema_version': 2, 'objects': [ { 'id': 'o', 'track_id': -4 } ] }");
            Assert.AreEqual(0, v2.Extras.Objects[0].TrackId);
            Assert.IsTrue(v2.Warnings.Any(w => w.Contains("negative")), string.Join(" | ", v2.Warnings));

            var pin = Pin("cup", "OTHER", "CvServer", -3, "s1");
            var v1 = ExtrasReader.Read(new JObject { ["schema_version"] = "1.0", ["pins"] = new JArray(pin) });
            Assert.AreEqual(0, v1.Extras.Objects[0].TrackId);
            Assert.AreEqual("pin-0", v1.Extras.Objects[0].Id, "an untracked pin is named by its place");
            Assert.IsTrue(v1.Warnings.Any(w => w.Contains("negative")), string.Join(" | ", v1.Warnings));
        }

        [TestCase("{ 'fx': 0, 'fy': 500, 'cx': 1, 'cy': 1 }")]
        [TestCase("{ 'fx': 500, 'fy': -2, 'cx': 1, 'cy': 1 }")]
        public void IntrinsicsWithAFocalLengthNotAboveZero_AreIgnored_WithAWarning(string intrinsics)
        {
            var result = ExtrasReader.Read("{ 'schema_version': 2, 'frames': [ { 'id': 'f', 'intrinsics': " + intrinsics + " } ] }");

            Assert.IsNull(result.Extras.Frames[0].Intrinsics);
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("focal length")), string.Join(" | ", result.Warnings));
        }

        [TestCase("{ 'agreed_at_utc': '2026-11-25T08:00:00Z' }", "notice_version")]
        [TestCase("{ 'notice_version': '2026-11' }", "agreed_at_utc")]
        public void AConsentWithoutItsNoticeVersionOrItsTime_WarnsWhichIsMissing(string consent, string missing)
        {
            var result = ExtrasReader.Read("{ 'schema_version': 2, 'consent': " + consent + " }");

            Assert.IsNotNull(result.Extras.Consent);
            Assert.AreEqual(1, result.Warnings.Count, string.Join(" | ", result.Warnings));
            StringAssert.Contains(missing, result.Warnings[0]);
        }

        [Test]
        public void APinWithoutAPosition_IsSkippedWithAWarning_AndKeptUnderV1()
        {
            var json = Models.GoldenV1();
            ((JArray)json["cv_mapping"]["pins"]).Add(JsonAssert.Parse("{ 'label': 'ghost', 'score': 0.4 }"));

            var result = ReadOkResult(json);

            Assert.AreEqual(2, result.Extras.Objects.Count);
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("pins[2]") && w.Contains("position")), string.Join(" | ", result.Warnings));
            Assert.AreEqual("ghost", (string)result.Extras.Unknown["v1"]["skipped_pins"][0]["label"]);
        }

        [Test]
        public void ASnapshotWithoutACameraAndAnAnchorWithoutAPosition_AreHandledLikewise()
        {
            var json = Models.ExporterShapedV1();
            ((JArray)json["cv_mapping"]["snapshots"]).Add(JsonAssert.Parse("{ 'id': 'snap_0002', 'timestamp': 1.0, 'intrinsics': [ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 ] }"));
            ((JArray)json["meta_scene"]["anchors"]).Add(JsonAssert.Parse("{ 'uuid': 'lost', 'labels': [ 'WALL_FACE' ] }"));

            var result = ReadOkResult(json);

            var frame = result.Extras.Frames.Single(f => f.Id == "snap_0002");
            Assert.IsNull(frame.Pose, "no camera, no pose");
            Assert.IsNull(frame.Intrinsics, "fx and fy must be above 0");
            Assert.AreEqual(1, result.Extras.Planes.Count, "the anchor without a position is skipped");
            Assert.AreEqual("lost", (string)result.Extras.Unknown["v1"]["skipped_anchors"][0]["uuid"]);
        }

        // ---- how a v1 pin's label becomes a label, a tag and a Meta label ----

        private static JObject Pin(string label, string semantic, string source = "CvServer", int track = 0, string session = "", string anchor = "")
        {
            return new JObject
            {
                ["label"] = label,
                ["semantic_label"] = semantic,
                ["score"] = 0.6,
                ["source"] = source,
                ["position"] = new JArray(1.0, 2.0, 3.0),
                ["track_id"] = track,
                ["capture_session_id"] = session,
                ["anchor_uuid"] = anchor
            };
        }

        private static ScanObject ReadPin(JObject pin)
        {
            var json = new JObject { ["schema_version"] = "1.0", ["pins"] = new JArray(pin) };
            return ReadOk(json.ToString()).Objects.Single();
        }

        // v1's own semantic_label was OTHER for most things; the vocabulary decides now, unless v1 knew better.
        [TestCase("chair", "OTHER", "chair", "OTHER")]
        [TestCase("picture frame", "OTHER", "wall art", "WALL_ART")]
        [TestCase("a couch", "OTHER", "couch", "COUCH")]
        [TestCase("TV screen", "OTHER", "screen", "SCREEN")]
        [TestCase("tables", "OTHER", "table", "TABLE")]
        [TestCase("TABLE", "TABLE", "table", "TABLE")]
        [TestCase("Foo", "STORAGE", "foo", "STORAGE")]
        [TestCase("", "TABLE", "table", "TABLE")]
        [TestCase("???", "OTHER", "", "OTHER")]
        [TestCase("unicorn", "", "unicorn", "OTHER")]
        [TestCase("couch", "NOT_A_LABEL", "couch", "COUCH")]
        public void V1Label_GivesTheTagAndTheMetaLabel(string label, string v1Semantic, string tag, string semantic)
        {
            var obj = ReadPin(Pin(label, v1Semantic));

            Assert.AreEqual(label, obj.Label.Text);
            Assert.AreEqual(tag, obj.Label.Tag);
            Assert.AreEqual(semantic, obj.Label.Semantic);
        }

        [TestCase("CvServer", ObjectSource.Cv)]
        [TestCase("MetaScene", ObjectSource.Device)]
        [TestCase("cv", ObjectSource.Cv)]
        [TestCase("device", ObjectSource.Device)]
        [TestCase("MANUAL", ObjectSource.Manual)]
        [TestCase("", ObjectSource.Cv)]
        public void V1Source_GivesTheV2Source(string source, ObjectSource expected)
        {
            Assert.AreEqual(expected, ReadPin(Pin("cup", "OTHER", source)).Source);
        }

        [Test]
        public void AnUnknownSource_IsReadAsCv_WithAWarning()
        {
            var json = new JObject { ["schema_version"] = "1.0", ["pins"] = new JArray(Pin("cup", "OTHER", "Robot")) };

            var result = ReadOkResult(json);

            Assert.AreEqual(ObjectSource.Cv, result.Extras.Objects[0].Source);
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("Robot")), string.Join(" | ", result.Warnings));
        }

        [TestCase("s1", 3, "", "s1-track-3")]
        [TestCase("", 3, "", "track-3")]
        [TestCase("s1", 0, "uuid-9", "anchor-uuid-9")]
        [TestCase("", 5, "uuid-9", "anchor-uuid-9")]
        [TestCase("s1", 0, "", "pin-0")]
        public void V1Pin_GetsAnIdThatIsStableAndUnique(string session, int track, string anchor, string expectedId)
        {
            Assert.AreEqual(expectedId, ReadPin(Pin("cup", "OTHER", "CvServer", track, session, anchor)).Id);
        }

        [Test]
        public void TwoPinsWithNoTrackAndNoAnchor_GetTheirIndexInTheirIds()
        {
            var json = new JObject { ["schema_version"] = "1.0", ["pins"] = new JArray(Pin("cup", "OTHER"), Pin("mug", "OTHER")) };

            CollectionAssert.AreEqual(new[] { "pin-0", "pin-1" }, ReadOk(json.ToString()).Objects.Select(o => o.Id).ToArray());
        }

        [Test]
        public void AVolumeBecomesTheObjectsBounds_CenteredOnItsPosition()
        {
            var pin = Pin("TABLE", "TABLE", "MetaScene", 0, "", "a1");
            pin["volume_size"] = new JArray(2.0, 1.0, 0.5);

            var obj = ReadPin(pin);

            AssertVec(new Vec3(1, 2, 3), obj.Bounds.Center);
            AssertVec(new Vec3(2, 1, 0.5), obj.Bounds.Size);
            Assert.IsFalse(obj.Unknown.ContainsKey("v1"), "the volume is the bounds now, so nothing is left over");
        }

        [Test]
        public void ABoxWithNoArea_GivesAnObservationWithoutABox()
        {
            var pin = Pin("cup", "OTHER");
            pin["capture_id"] = "snap_0003";
            pin["bbox_normalized"] = new JArray(0.0, 0.0, 0.0, 0.0);

            var obj = ReadPin(pin);

            var seen = obj.Observations.Single();
            Assert.AreEqual("snap_0003", seen.FrameId);
            Assert.IsFalse(seen.Box.HasValue);
        }

        [Test]
        public void ABoxPastTheImage_IsClamped_NotReadAsPixels()
        {
            var pin = Pin("cup", "OTHER");
            pin["capture_id"] = "snap_0003";
            pin["bbox_normalized"] = new JArray(0.5, 0.5, 0.5001, 0.2);

            var box = ReadPin(pin).Observations.Single().Box.Value;

            Assert.AreEqual(0.5, box.X1, Tolerance);
            Assert.AreEqual(1.0, box.X2, Tolerance, "x + width is 1.0001: clamped to the image, where the scanner's guess made a speck");
            Assert.AreEqual(0.7, box.Y2, Tolerance);
        }

        [Test]
        public void AnObjectWithoutAScore_HasConfidenceZero_AndAScoreOutsideZeroToOneIsClamped()
        {
            var noScore = Pin("cup", "OTHER");
            noScore.Remove("score");
            Assert.AreEqual(0.0, ReadPin(noScore).Confidence);

            var tooBig = Pin("cup", "OTHER");
            tooBig["score"] = 7.5;
            Assert.AreEqual(1.0, ReadPin(tooBig).Confidence);

            var negative = Pin("cup", "OTHER");
            negative["score"] = -2.0;
            Assert.AreEqual(0.0, ReadPin(negative).Confidence);
        }

        [Test]
        public void IntrinsicsOfAnIdentityMatrix_AreReadAsTheyAre_ForTheScannerToFix()
        {
            // The scanner's HasIntrinsics is true for Matrix4x4.identity (m00 and m11 above 0); v1 files can hold that.
            var json = Models.GoldenV1();
            ((JObject)json["cv_mapping"]["snapshots"][0])["intrinsics"] = JsonAssert.Parse("[ 1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 ]");

            var intrinsics = ReadOk(json.ToString()).Frames[0].Intrinsics;

            Assert.AreEqual(1.0, intrinsics.Fx);
            Assert.AreEqual(1.0, intrinsics.Fy);
            Assert.AreEqual(0.0, intrinsics.Cx);
            Assert.AreEqual(0.0, intrinsics.Cy);
        }

        // ---- v2 ----



        [Test]
        public void V2_ReadsEveryPart()
        {
            var result = ExtrasReader.Read(Models.V2Document);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2, result.SourceVersion);
            Assert.IsEmpty(result.Warnings);
            var x = result.Extras;

            Assert.AreEqual("room-9", x.Space.Id);
            Assert.AreEqual("unity-world", x.Space.Frame);
            Assert.AreEqual("2026-11-24T08:00:00Z", x.Space.CreatedAtUtc);
            Assert.AreEqual("2026-11-25T10:00:00Z", x.Space.UpdatedAtUtc);
            Assert.AreEqual("2026-11", x.Consent.NoticeVersion);
            Assert.AreEqual("2026-11-24T07:59:00Z", x.Consent.AgreedAtUtc);
            Assert.AreEqual("https://scan.example.test", x.Consent.Server);
            Assert.AreEqual("Meta Quest 3", x.Device.Model);
            Assert.AreEqual("Horizon OS 78", x.Device.Os);
            Assert.AreEqual("companion-scan", x.Device.App);
            Assert.AreEqual("1.4.0", x.Device.AppVersion);

            var frame = x.Frames.Single();
            Assert.AreEqual("snap_0001", frame.Id);
            Assert.AreEqual("2026-11-24T08:30:00Z", frame.TimestampUtc);
            Assert.AreEqual("snapshots/snap_0001.jpg", frame.Image);
            AssertVec(new Vec3(0.5, 1.5, -1), frame.Pose.Position);
            Assert.AreEqual(new Quat(0.125, 0.25, 0.375, 0.5), frame.Pose.Rotation);
            Assert.AreEqual(600.0, frame.Intrinsics.Fx);
            Assert.AreEqual(601.0, frame.Intrinsics.Fy);
            Assert.AreEqual(320.0, frame.Intrinsics.Cx);
            Assert.AreEqual(240.0, frame.Intrinsics.Cy);
            Assert.AreEqual(640, frame.Intrinsics.Width);
            Assert.AreEqual(480, frame.Intrinsics.Height);

            var obj = x.Objects.Single();
            Assert.AreEqual("s1-track-1", obj.Id);
            Assert.AreEqual(1, obj.TrackId);
            Assert.AreEqual("TV screen", obj.Label.Text);
            Assert.AreEqual("screen", obj.Label.Tag);
            Assert.AreEqual("SCREEN", obj.Label.Semantic);
            Assert.AreEqual(0.75, obj.Confidence);
            Assert.AreEqual(ObjectSource.Cv, obj.Source);
            AssertVec(new Vec3(1, 0.5, 2), obj.Pose.Position);
            Assert.AreEqual(Quat.Identity, obj.Pose.Rotation);
            Assert.AreEqual(new Vec3(0, 1, 0), obj.Normal.Value);
            AssertVec(new Vec3(1, 0.5, 2), obj.Bounds.Center);
            AssertVec(new Vec3(1, 0.6, 0.1), obj.Bounds.Size);
            Assert.AreEqual("a1", obj.AnchorUuid);
            var seen = obj.Observations.Single();
            Assert.AreEqual("snap_0001", seen.FrameId);
            Assert.AreEqual(0.75, seen.Score);
            Assert.AreEqual(0.1, seen.Box.Value.X1, Tolerance);
            Assert.AreEqual(0.2, seen.Box.Value.Y1, Tolerance);
            Assert.AreEqual(0.5, seen.Box.Value.X2, Tolerance);
            Assert.AreEqual(0.8, seen.Box.Value.Y2, Tolerance);

            var plane = x.Planes.Single();
            Assert.AreEqual("a2", plane.Id);
            Assert.AreEqual("wall", plane.Label);
            Assert.AreEqual("WALL_FACE", plane.Semantic);
            AssertVec(new Vec3(2, 1.25, 0), plane.Pose.Position);
            Assert.AreEqual(new Vec2(0, 0), plane.Center.Value);
            Assert.AreEqual(new Vec2(4, 2.5), plane.Size.Value);
            CollectionAssert.AreEqual(new[] { new Vec3(0, 0, 0), new Vec3(4, 0, 0) }, plane.Boundary);

            AssertVec(new Vec3(2, 1.25, 1), x.Bounds.Center);
            AssertVec(new Vec3(4, 2.5, 2), x.Bounds.Size);
        }

        [Test]
        public void V2_KeepsUnknownKeysAtEveryLevel()
        {
            var x = ReadOk(Models.V2Document);

            Assert.AreEqual(2, ((JArray)x.Unknown["future_root_key"]["a"]).Count);
            Assert.AreEqual(7, (int)x.Space.Unknown["future_space_key"]);
            Assert.IsTrue((bool)x.Consent.Unknown["future_consent_key"]);
            Assert.AreEqual("x", (string)x.Device.Unknown["future_device_key"]);
            Assert.AreEqual("f", (string)x.Frames[0].Unknown["future_frame_key"]);
            Assert.AreEqual("local-floor", (string)x.Frames[0].Pose.Unknown["frame"]);
            Assert.AreEqual(0.1, (double)x.Frames[0].Intrinsics.Unknown["distortion"][0], Tolerance);
            Assert.AreEqual(1, (int)x.Objects[0].Label.Unknown["future_label_key"]);
            Assert.AreEqual(0, (int)x.Objects[0].Observations[0].Unknown["future_observation_key"]);
            Assert.AreEqual(1, (int)x.Objects[0].Unknown["future_object_key"][0]);
            Assert.IsTrue(x.Planes[0].Unknown.ContainsKey("future_plane_key"), "a null value is a value");
            Assert.AreEqual("b", (string)x.Bounds.Unknown["future_bounds_key"]);

            // and only the unknown keys are in there
            AssertKeys(x.Unknown, "future_root_key");
            AssertKeys(x.Space.Unknown, "future_space_key");
            AssertKeys(x.Objects[0].Unknown, "future_object_key");
        }

        [Test]
        public void V2_WithOnlyTheRequiredParts_ReadsAnEmptyDocument()
        {
            var result = ExtrasReader.Read("{ 'schema_version': 2, 'space': { 'id': 'r' } }");

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("r", result.Extras.Space.Id);
            Assert.AreEqual("unity-world", result.Extras.Space.Frame);
            Assert.IsEmpty(result.Extras.Frames);
            Assert.IsEmpty(result.Extras.Objects);
            Assert.IsEmpty(result.Extras.Planes);
            Assert.IsNull(result.Extras.Bounds);
            Assert.IsEmpty(result.Warnings);
        }

        [Test]
        public void TheReader_DoesNotChangeTheJsonItIsGiven()
        {
            var json = JsonAssert.ParseObject(Models.V2Document);
            var before = json.DeepClone();
            ExtrasReader.Read(json);
            Assert.IsTrue(JToken.DeepEquals(before, json), "a v2 document");

            var v1 = Models.ExporterShapedV1();
            var v1Before = v1.DeepClone();
            ExtrasReader.Read(v1);
            Assert.IsTrue(JToken.DeepEquals(v1Before, v1), "a v1 document");
        }

        [Test]
        public void ADateThatTheCallerParsedAsADate_IsReadBackAsItsText()
        {
            // JObject.Parse turns an ISO string into a date; the reader must not lose it.
            var parsed = JObject.Parse("{ 'schema_version': 2, 'space': { 'id': 'r', 'updated_at_utc': '2026-11-25T10:00:00Z' } }");
            Assert.AreEqual(JTokenType.Date, parsed["space"]["updated_at_utc"].Type, "the precondition: it is a date");

            var updated = ReadOkResult(parsed).Extras.Space.UpdatedAtUtc;

            Assert.IsTrue(DateTimeOffset.TryParse(updated, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant), updated);
            Assert.AreEqual(new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero), instant);
        }

        // ---- a v2 file that is a little off: read what can be read ----

        [Test]
        public void V2_AcceptsArraysForPositionsAndRotationAsTheNameOfAQuaternion()
        {
            var json = @"{ 'schema_version': 2, 'objects': [ {
                'id': 'o', 'pose': { 'position': [ 1, 2, 3 ], 'rotation': [ 0.1, 0.2, 0.3, 0.9 ] }, 'normal': [ 0, 0, 1 ],
                'bounds': { 'center': [ 1, 1, 1 ], 'size': [ 2, 2, 2 ] }
            } ], 'planes': [ { 'id': 'p', 'center': [ 1, 2 ], 'size': [ 3, 4 ], 'pose': { 'position': [ 0, 0, 0 ] }, 'boundary': [ [ 1, 2, 3 ] ] } ] }";

            var x = ReadOk(json);

            AssertVec(new Vec3(1, 2, 3), x.Objects[0].Pose.Position);
            Assert.AreEqual(new Quat(0.1, 0.2, 0.3, 0.9), x.Objects[0].Pose.Rotation);
            Assert.AreEqual(new Vec3(0, 0, 1), x.Objects[0].Normal.Value);
            AssertVec(new Vec3(2, 2, 2), x.Objects[0].Bounds.Size);
            Assert.AreEqual(new Vec2(1, 2), x.Planes[0].Center.Value);
            Assert.AreEqual(new Vec2(3, 4), x.Planes[0].Size.Value);
            Assert.AreEqual(Quat.Identity, x.Planes[0].Pose.Rotation, "no rotation means none");
            Assert.AreEqual(new Vec3(1, 2, 3), x.Planes[0].Boundary.Single());
        }

        [Test]
        public void V2_ALabelThatIsPlainText_GetsATagAndAMetaLabel()
        {
            var x = ReadOk("{ 'schema_version': 2, 'objects': [ { 'id': 'o', 'label': 'a Sofa Bed' } ] }");

            Assert.AreEqual("a Sofa Bed", x.Objects[0].Label.Text);
            Assert.AreEqual("couch", x.Objects[0].Label.Tag);
            Assert.AreEqual("COUCH", x.Objects[0].Label.Semantic);
        }

        [TestCase("{ 'text': 'monitor' }", "monitor", "screen", "SCREEN", false)]
        [TestCase("{ 'text': 'monitor', 'tag': 'screen' }", "monitor", "screen", "SCREEN", false)]
        [TestCase("{ 'tag': 'couch' }", "couch", "couch", "COUCH", false)]
        [TestCase("{ 'text': 'sofa', 'tag': 'couch', 'semantic': 'COUCH' }", "sofa", "couch", "COUCH", false)]
        [TestCase("{ 'text': 'sofa', 'tag': 'couch', 'semantic': 'chair' }", "sofa", "couch", "COUCH", true)]
        [TestCase("{ 'text': 'sofa', 'tag': 'couch', 'semantic': 7 }", "sofa", "couch", "COUCH", true)]
        public void V2Label_FillsWhatIsMissing_AndWarnsAboutWhatIsWrong(string label, string text, string tag, string semantic, bool warns)
        {
            var result = ExtrasReader.Read("{ 'schema_version': 2, 'objects': [ { 'id': 'o', 'label': " + label + " } ] }");

            Assert.IsTrue(result.Success, result.Error);
            var read = result.Extras.Objects[0].Label;
            Assert.AreEqual(text, read.Text);
            Assert.AreEqual(tag, read.Tag);
            Assert.AreEqual(semantic, read.Semantic);
            Assert.AreEqual(warns, result.Warnings.Count > 0, string.Join(" | ", result.Warnings));
        }

        [Test]
        public void V2_ObjectsAndFramesWithoutAnId_GetOneFromTheirPlace_WithAWarning()
        {
            var result = ExtrasReader.Read("{ 'schema_version': 2, 'frames': [ { 'id': 'f' }, { } ], 'objects': [ { 'id': 'a' }, { }, { } ], 'planes': [ { } ] }");

            Assert.IsTrue(result.Success, result.Error);
            CollectionAssert.AreEqual(new[] { "f", "frame-1" }, result.Extras.Frames.Select(f => f.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "a", "object-1", "object-2" }, result.Extras.Objects.Select(o => o.Id).ToArray());
            Assert.AreEqual("plane-0", result.Extras.Planes[0].Id);
            Assert.AreEqual(4, result.Warnings.Count(w => w.Contains("no id")), string.Join(" | ", result.Warnings));
        }

        [Test]
        public void V2_TheSameIdTwice_IsOneEntry_TheLaterOneInThePlaceOfTheFirst()
        {
            var result = ExtrasReader.Read(@"{ 'schema_version': 2, 'objects': [
                { 'id': 'a', 'confidence': 0.1 }, { 'id': 'b' }, { 'id': 'a', 'confidence': 0.9 } ] }");

            Assert.IsTrue(result.Success, result.Error);
            CollectionAssert.AreEqual(new[] { "a", "b" }, result.Extras.Objects.Select(o => o.Id).ToArray());
            Assert.AreEqual(0.9, result.Extras.Objects[0].Confidence);
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("listed more than once")), string.Join(" | ", result.Warnings));
        }

        [TestCase("0.5", 0.5, false)]
        [TestCase("1.5", 1.0, false)]
        [TestCase("-3", 0.0, false)]
        [TestCase("'high'", 0.0, true)]
        [TestCase("null", 0.0, true)]
        [TestCase("NaN", 0.0, true)]
        public void V2Confidence_IsClamped_AndAWrongTypeIsReadAsZeroWithAWarning(string value, double expected, bool warns)
        {
            var result = ExtrasReader.Read("{ 'schema_version': 2, 'objects': [ { 'id': 'o', 'confidence': " + value + " } ] }");

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(expected, result.Extras.Objects[0].Confidence);
            Assert.AreEqual(warns, result.Warnings.Count > 0, string.Join(" | ", result.Warnings));
        }

        [TestCase("7", 7, false)]
        [TestCase("7.0", 7, false)]
        [TestCase("'7'", 7, false)]
        [TestCase("'1-0'", 0, true)]
        [TestCase("7.5", 0, true)]
        [TestCase("null", 0, true)]
        public void V2TrackId_IsAnIntegerOrNumericText(string value, int expected, bool warns)
        {
            var result = ExtrasReader.Read("{ 'schema_version': 2, 'objects': [ { 'id': 'o', 'track_id': " + value + " } ] }");

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(expected, result.Extras.Objects[0].TrackId);
            Assert.AreEqual(warns, result.Warnings.Count > 0, string.Join(" | ", result.Warnings));
        }

        [Test]
        public void ANumberFarTooBigForAnInt_IsAWarning_NotAnException()
        {
            var result = ExtrasReader.Read("{ 'schema_version': 2, 'objects': [ { 'id': 'o', 'track_id': 123456789012345678901234567890, 'confidence': 123456789012345678901234567890 } ] }");

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0, result.Extras.Objects[0].TrackId);
            Assert.AreEqual(1.0, result.Extras.Objects[0].Confidence, "a huge confidence is clamped");
            Assert.AreEqual(1, result.Warnings.Count, string.Join(" | ", result.Warnings));
            StringAssert.Contains("track_id", result.Warnings[0]);
        }

        [TestCase("'cv'", ObjectSource.Cv, false)]
        [TestCase("'device'", ObjectSource.Device, false)]
        [TestCase("'manual'", ObjectSource.Manual, false)]
        [TestCase("'Manual'", ObjectSource.Manual, false)]
        [TestCase("'robot'", ObjectSource.Cv, true)]
        [TestCase("5", ObjectSource.Cv, true)]
        public void V2Source_IsOneOfThree_AndAnythingElseIsReadAsCvWithAWarning(string value, ObjectSource expected, bool warns)
        {
            var result = ExtrasReader.Read("{ 'schema_version': 2, 'objects': [ { 'id': 'o', 'source': " + value + " } ] }");

            Assert.AreEqual(expected, result.Extras.Objects[0].Source);
            Assert.AreEqual(warns, result.Warnings.Count > 0, string.Join(" | ", result.Warnings));
        }

        [Test]
        public void V2_APartOfTheWrongType_IsIgnoredWithAWarning()
        {
            var result = ExtrasReader.Read(@"{ 'schema_version': 2,
                'space': 5, 'consent': [], 'device': 'phone', 'frames': { }, 'objects': 'many', 'planes': 7, 'bounds': 'wide' }");

            Assert.IsTrue(result.Success, result.Error);
            var x = result.Extras;
            Assert.AreEqual("default", x.Space.Id);
            Assert.IsNull(x.Consent);
            Assert.IsNull(x.Device);
            Assert.IsEmpty(x.Frames);
            Assert.IsEmpty(x.Objects);
            Assert.IsEmpty(x.Planes);
            Assert.IsNull(x.Bounds);
            Assert.AreEqual(7, result.Warnings.Count, string.Join(" | ", result.Warnings));
        }

        [Test]
        public void V2_ListItemsThatAreNotObjects_AreSkippedWithAWarning()
        {
            var result = ExtrasReader.Read("{ 'schema_version': 2, 'objects': [ 5, { 'id': 'a' }, null, 'x' ], 'frames': [ [ ] ] }");

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Extras.Objects.Count);
            Assert.IsEmpty(result.Extras.Frames);
            Assert.AreEqual(4, result.Warnings.Count, string.Join(" | ", result.Warnings));
        }

        [Test]
        public void V2_AnObservationBoxThatIsNotFourNumbers_IsLeftOut_WithAWarning()
        {
            var result = ExtrasReader.Read(@"{ 'schema_version': 2, 'objects': [ { 'id': 'o', 'observations': [
                { 'frame_id': 'f1', 'bbox_cxcywh': [ 0.5, 0.5, 0.2 ], 'score': 0.5 },
                { 'frame_id': 'f2', 'bbox_cxcywh': [ 0.5, 0.5, 0.0, 0.2 ], 'score': 0.5 },
                { 'frame_id': 'f3', 'score': 0.5 } ] } ] }");

            var seen = result.Extras.Objects[0].Observations;
            Assert.AreEqual(3, seen.Count);
            Assert.IsTrue(seen.All(s => !s.Box.HasValue));
            Assert.AreEqual(2, result.Warnings.Count, string.Join(" | ", result.Warnings));
        }

        [Test]
        public void V2_APoseWithoutAPosition_IsLeftOut_WithAWarning()
        {
            var result = ExtrasReader.Read("{ 'schema_version': 2, 'frames': [ { 'id': 'f', 'pose': { 'quaternion': { 'x': 0, 'y': 0, 'z': 0, 'w': 1 } } } ], 'objects': [ { 'id': 'o', 'pose': { } } ] }");

            Assert.IsNull(result.Extras.Frames[0].Pose);
            AssertVec(Vec3.Zero, result.Extras.Objects[0].Pose.Position);
            Assert.AreEqual(2, result.Warnings.Count, string.Join(" | ", result.Warnings));
        }

        // ---- which version a document is ----

        [TestCase("{ 'schema_version': '1.0', 'cv_mapping': { } }", 1, false)]
        [TestCase("{ 'schema_version': 1, 'pins': [ ] }", 1, false)]
        [TestCase("{ 'schema_version': '1.2.3', 'pins': [ ] }", 1, false)]
        [TestCase("{ 'schema': 'companion.scan-extras', 'schema_version': 2 }", 2, false)]
        [TestCase("{ 'schema_version': 2 }", 2, false)]
        [TestCase("{ 'schema_version': '2' }", 2, false)]
        [TestCase("{ 'schema_version': '2.0' }", 2, false)]
        [TestCase("{ 'schema': 'companion.scan-extras' }", 2, false)]
        [TestCase("{ 'cv_mapping': { 'pins': [ ] } }", 1, true)]
        [TestCase("{ 'pins': [ ] }", 1, true)]
        [TestCase("{ 'meta_scene': { 'available': false } }", 1, true)]
        [TestCase("{ 'schema_version': 0, 'pins': [ ] }", 1, true)]
        [TestCase("{ 'schema_version': '0.9', 'pins': [ ] }", 1, true)]
        [TestCase("{ 'schema_version': 3, 'objects': [ ] }", 3, true)]
        [TestCase("{ 'schema_version': '3.1', 'objects': [ ] }", 3, true)]
        public void TheVersion_IsReadFromSchemaVersion_OrGuessedFromTheShape(string json, int version, bool warns)
        {
            var result = ExtrasReader.Read(json);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(version, result.SourceVersion);
            Assert.AreEqual(warns, result.Warnings.Count > 0, string.Join(" | ", result.Warnings));
        }

        [Test]
        public void ANewerVersion_IsReadAsV2_KeepingItsOtherKeys()
        {
            var result = ExtrasReader.Read("{ 'schema_version': 3, 'space': { 'id': 'r' }, 'objects': [ { 'id': 'o', 'hologram': true } ], 'cloud': { 'id': 1 } }");

            Assert.IsTrue(result.Success, result.Error);
            StringAssert.Contains("3", result.Warnings[0]);
            Assert.AreEqual("o", result.Extras.Objects[0].Id);
            Assert.IsTrue((bool)result.Extras.Objects[0].Unknown["hologram"]);
            Assert.AreEqual(1, (int)result.Extras.Unknown["cloud"]["id"]);
        }

        // ---- corrupt input: an error, never an exception ----

        private static string Deep(int levels)
        {
            return new StringBuilder().Insert(0, "[", levels).Append(new string(']', levels)).ToString();
        }

        private static readonly object[][] CorruptDocuments =
        {
            new object[] { null, "empty" },
            new object[] { "", "empty" },
            new object[] { "   \r\n\t ", "empty" },
            new object[] { "{", "not valid JSON" },
            new object[] { "{ \"asset\": ", "not valid JSON" },
            new object[] { "{ \"a\": 1 } trailing", "not valid JSON" },
            new object[] { "{ \"a\": 1 }{ \"b\": 2 }", "not valid JSON" },
            new object[] { "not json at all", "not valid JSON" },
            new object[] { "{ 'a': }", "not valid JSON" },
            new object[] { "[]", "not a JSON object" },
            new object[] { "[ { \"schema_version\": 2 } ]", "not a JSON object" },
            new object[] { "5", "not a JSON object" },
            new object[] { "\"text\"", "not a JSON object" },
            new object[] { "true", "not a JSON object" },
            new object[] { "null", "not a JSON object" },
            new object[] { "{}", "not a Companion scan extras" },
            new object[] { "{ \"foo\": 1 }", "not a Companion scan extras" },
            new object[] { "{ \"schema_version\": \"abc\" }", "not a Companion scan extras" },
            new object[] { "{ \"schema_version\": -1 }", "not a Companion scan extras" },
            new object[] { "{ \"schema_version\": null }", "not a Companion scan extras" }
        };

        private static System.Collections.Generic.IEnumerable<TestCaseData> CorruptCases()
        {
            for (int i = 0; i < CorruptDocuments.Length; i++)
            {
                yield return new TestCaseData(CorruptDocuments[i][0], CorruptDocuments[i][1]).SetName("Corrupt_" + (i + 1).ToString("00"));
            }
        }

        [TestCaseSource(nameof(CorruptCases))]
        public void ACorruptDocument_GivesAClearError_NotAnException(string json, string expectedError)
        {
            ExtrasReadResult result = null;
            Assert.DoesNotThrow(() => result = ExtrasReader.Read(json));

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Extras);
            StringAssert.Contains(expectedError, result.Error);
            Assert.IsNotNull(result.Warnings);
        }

        [Test]
        public void ADocumentNestedTooDeep_GivesAnError_NotAStackOverflow()
        {
            ExtrasReadResult result = null;
            Assert.DoesNotThrow(() => result = ExtrasReader.Read(Deep(5000)));

            Assert.IsFalse(result.Success);
            StringAssert.Contains("not valid JSON", result.Error);
        }

        [Test]
        public void ANullObject_GivesAnError()
        {
            var result = ExtrasReader.Read((JObject)null);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("null", result.Error);
        }

        [Test]
        public void ABomInFrontOfTheJson_IsIgnored()
        {
            var result = ExtrasReader.Read("\uFEFF" + Models.V2Document);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("room-9", result.Extras.Space.Id);
        }

        [Test]
        public void ASuccessfulResult_HasNoError_AndAFailedOneHasNoDocument()
        {
            var ok = ExtrasReader.Read(Models.V2Document);
            var failed = ExtrasReader.Read("{");

            Assert.IsTrue(ok.Success);
            Assert.IsNull(ok.Error);
            Assert.IsNotNull(ok.Extras);
            Assert.IsFalse(failed.Success);
            Assert.IsNotNull(failed.Error);
            Assert.IsNull(failed.Extras);
            Assert.AreEqual(0, failed.SourceVersion);
        }

        [Test]
        public void ACustomVocabulary_DecidesTheTagsOfWhatIsDerived()
        {
            var vocabulary = new Companion.Spatial.Vocabulary.TagVocabulary(new[]
            {
                new Companion.Spatial.Vocabulary.TagEntry("widget", "TABLE", "gizmo")
            });

            var result = ExtrasReader.Read("{ 'schema_version': 2, 'objects': [ { 'id': 'o', 'label': 'Gizmos' } ] }", vocabulary);

            Assert.AreEqual("widget", result.Extras.Objects[0].Label.Tag);
            Assert.AreEqual("TABLE", result.Extras.Objects[0].Label.Semantic);
        }
    }
}
