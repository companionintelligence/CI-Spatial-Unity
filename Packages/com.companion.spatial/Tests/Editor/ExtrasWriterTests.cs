// Tests for ExtrasWriter: the v2 document, its stable key order, lists that are never null, and a document that
// satisfies the schema.

using System;
using System.Linq;
using Companion.Spatial.Extras;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Companion.Spatial.Tests
{
    public class ExtrasWriterTests
    {
        private static void AssertKeyOrder(JToken node, params string[] expected)
        {
            CollectionAssert.AreEqual(expected, JsonAssert.Keys(node), "the keys of " + node.Path);
        }

        // ---- the document ----

        [Test]
        public void Write_TheFullModel_GivesTheDocumentOfTheContract()
        {
            var written = ExtrasWriter.Write(Models.Full());

            JsonAssert.AreSame(JsonAssert.ParseObject(@"{
                'schema': 'companion.scan-extras', 'schema_version': 2, 'units': 'meters',
                'space': { 'id': 'room-1', 'frame': 'unity-world', 'created_at_utc': '2026-11-25T09:00:00.0000000Z', 'updated_at_utc': '2026-11-25T10:00:00.0000000Z' },
                'consent': { 'notice_version': '2026-11', 'agreed_at_utc': '2026-11-25T08:55:00.0000000Z', 'server': 'https://scan.example.test' },
                'device': { 'model': 'Meta Quest 3', 'os': 'Horizon OS 78', 'app': 'companion-scan', 'app_version': '1.4.0' },
                'frames': [ {
                    'id': 'snap_0001', 'timestamp_utc': '2026-11-25T09:30:00.0000000Z', 'image': 'snapshots/snap_0001.jpg',
                    'pose': { 'position': { 'x': 0.5, 'y': 1.5, 'z': -1 }, 'quaternion': { 'x': 0.125, 'y': 0.25, 'z': 0.375, 'w': 0.5 } },
                    'intrinsics': { 'fx': 600, 'fy': 600, 'cx': 320, 'cy': 240, 'width': 640, 'height': 480 }
                } ],
                'objects': [
                    { 'id': 's1-track-1', 'track_id': 1, 'label': { 'text': 'TV screen', 'tag': 'screen', 'semantic': 'SCREEN' },
                      'confidence': 0.75, 'source': 'cv',
                      'pose': { 'position': { 'x': 1, 'y': 0.5, 'z': 2 }, 'quaternion': { 'x': 0, 'y': 0, 'z': 0, 'w': 1 } },
                      'normal': { 'x': 0, 'y': 1, 'z': 0 },
                      'observations': [ { 'frame_id': 'snap_0001', 'bbox_cxcywh': [ 0.3, 0.5, 0.4, 0.6 ], 'score': 0.75 } ] },
                    { 'id': 'anchor-a1', 'track_id': 0, 'label': { 'text': 'TABLE', 'tag': 'table', 'semantic': 'TABLE' },
                      'confidence': 1, 'source': 'device',
                      'pose': { 'position': { 'x': 1, 'y': 0.75, 'z': 3.5 }, 'quaternion': { 'x': 0, 'y': 0, 'z': 0, 'w': 1 } },
                      'bounds': { 'center': { 'x': 1, 'y': 0.75, 'z': 3.5 }, 'size': { 'x': 1.5, 'y': 0.75, 'z': 0.6 } },
                      'anchor_uuid': 'a1', 'observations': [] }
                ],
                'planes': [ {
                    'id': 'a2', 'label': 'wall', 'semantic': 'WALL_FACE',
                    'pose': { 'position': { 'x': 2, 'y': 1.25, 'z': 0 }, 'quaternion': { 'x': 0, 'y': 0, 'z': 0, 'w': 1 } },
                    'center': { 'x': 0, 'y': 0 }, 'size': { 'x': 4, 'y': 2.5 },
                    'boundary': [ { 'x': 0, 'y': 0, 'z': 0 }, { 'x': 4, 'y': 0, 'z': 0 }, { 'x': 4, 'y': 2.5, 'z': 0 }, { 'x': 0, 'y': 2.5, 'z': 0 } ]
                } ],
                'bounds': { 'center': { 'x': 2, 'y': 1.25, 'z': 1.9 }, 'size': { 'x': 4, 'y': 2.5, 'z': 3.8 } }
            }"), written, "the written document");
        }

        [Test]
        public void Write_KeepsAStableKeyOrder()
        {
            var written = ExtrasWriter.Write(Models.Full());

            AssertKeyOrder(written, "schema", "schema_version", "units", "space", "consent", "device", "frames", "objects", "planes", "bounds");
            AssertKeyOrder(written["space"], "id", "frame", "created_at_utc", "updated_at_utc");
            AssertKeyOrder(written["consent"], "notice_version", "agreed_at_utc", "server");
            AssertKeyOrder(written["device"], "model", "os", "app", "app_version");
            AssertKeyOrder(written["frames"][0], "id", "timestamp_utc", "image", "pose", "intrinsics");
            AssertKeyOrder(written["frames"][0]["pose"], "position", "quaternion");
            AssertKeyOrder(written["frames"][0]["pose"]["position"], "x", "y", "z");
            AssertKeyOrder(written["frames"][0]["pose"]["quaternion"], "x", "y", "z", "w");
            AssertKeyOrder(written["frames"][0]["intrinsics"], "fx", "fy", "cx", "cy", "width", "height");
            AssertKeyOrder(written["objects"][0], "id", "track_id", "label", "confidence", "source", "pose", "normal", "observations");
            AssertKeyOrder(written["objects"][1], "id", "track_id", "label", "confidence", "source", "pose", "bounds", "anchor_uuid", "observations");
            AssertKeyOrder(written["objects"][0]["label"], "text", "tag", "semantic");
            AssertKeyOrder(written["objects"][0]["observations"][0], "frame_id", "bbox_cxcywh", "score");
            AssertKeyOrder(written["objects"][1]["bounds"], "center", "size");
            AssertKeyOrder(written["planes"][0], "id", "label", "semantic", "pose", "center", "size", "boundary");
            AssertKeyOrder(written["planes"][0]["center"], "x", "y");
            AssertKeyOrder(written["bounds"], "center", "size");
        }

        [Test]
        public void Write_TheSameModelTwice_GivesTheSameText()
        {
            Assert.AreEqual(ExtrasWriter.ToJson(Models.Full()), ExtrasWriter.ToJson(Models.Full()));
            Assert.AreEqual(ExtrasWriter.ToJson(Models.Full(), false), ExtrasWriter.ToJson(Models.Full(), false));
        }

        [Test]
        public void Write_AnEmptyModel_HasEmptyListsAndNothingElse()
        {
            var written = ExtrasWriter.Write(new ScanExtras());

            AssertKeyOrder(written, "schema", "schema_version", "units", "space", "frames", "objects", "planes");
            JsonAssert.AreSame(JsonAssert.ParseObject(@"{
                'schema': 'companion.scan-extras', 'schema_version': 2, 'units': 'meters',
                'space': { 'id': 'default', 'frame': 'unity-world' }, 'frames': [], 'objects': [], 'planes': []
            }"), written, "an empty document");
        }

        // v1 wrote "pins": null when there was nothing to write.
        [Test]
        public void Write_NeverWritesNull_AndEveryListIsAnArray()
        {
            foreach (var pair in Models.ValidModels())
            {
                var written = ExtrasWriter.Write(pair.Value);

                Assert.IsEmpty(JsonAssert.Nulls(written), pair.Key + ": " + string.Join(", ", JsonAssert.Nulls(written)));
                Assert.AreEqual(JTokenType.Array, written["frames"].Type, pair.Key);
                Assert.AreEqual(JTokenType.Array, written["objects"].Type, pair.Key);
                Assert.AreEqual(JTokenType.Array, written["planes"].Type, pair.Key);
                foreach (var obj in written["objects"])
                {
                    Assert.AreEqual(JTokenType.Array, obj["observations"].Type, pair.Key + " observations");
                }

                foreach (var plane in written["planes"])
                {
                    Assert.AreEqual(JTokenType.Array, plane["boundary"].Type, pair.Key + " boundary");
                }
            }
        }

        [Test]
        public void Write_LeavesOutWhatIsNotThere()
        {
            var extras = new ScanExtras();
            extras.Frames.Add(new ScanFrame { Id = "f" });
            extras.Objects.Add(new ScanObject { Id = "o" });
            extras.Planes.Add(new ScanPlane { Id = "p" });
            extras.Consent = new ScanConsent { NoticeVersion = "v1" };
            extras.Device = new ScanDevice { App = "a" };

            var written = ExtrasWriter.Write(extras);

            AssertKeyOrder(written["frames"][0], "id");
            AssertKeyOrder(written["objects"][0], "id", "track_id", "label", "confidence", "source", "pose", "observations");
            AssertKeyOrder(written["planes"][0], "id", "label", "semantic", "pose", "boundary");
            AssertKeyOrder(written["consent"], "notice_version");
            AssertKeyOrder(written["device"], "app");
            Assert.IsNull(written["bounds"]);
        }

        [Test]
        public void Write_AnImageSizeOfZero_IsLeftOutOfTheIntrinsics()
        {
            var extras = new ScanExtras();
            extras.Frames.Add(new ScanFrame { Id = "f", Intrinsics = new ScanIntrinsics { Fx = 500, Fy = 500, Cx = 1, Cy = 2 } });

            AssertKeyOrder(ExtrasWriter.Write(extras)["frames"][0]["intrinsics"], "fx", "fy", "cx", "cy");
        }

        [Test]
        public void Write_AnObservationWithoutABox_HasNoBboxKey()
        {
            var extras = new ScanExtras();
            var obj = new ScanObject { Id = "o" };
            obj.Observations.Add(new ScanObservation { FrameId = "f", Score = 0.4 });
            extras.Objects.Add(obj);

            AssertKeyOrder(ExtrasWriter.Write(extras)["objects"][0]["observations"][0], "frame_id", "score");
        }

        [Test]
        public void Write_ABoxIsWrittenAsCentreAndSize()
        {
            var extras = new ScanExtras();
            var obj = new ScanObject { Id = "o" };
            NormalizedBox.TryFromXywh(0.0625, 0.125, 0.25, 0.5, out var box);
            obj.Observations.Add(new ScanObservation { FrameId = "f", Box = box, Score = 0.4 });
            extras.Objects.Add(obj);

            var cxcywh = (JArray)ExtrasWriter.Write(extras)["objects"][0]["observations"][0]["bbox_cxcywh"];

            CollectionAssert.AreEqual(new[] { 0.1875, 0.375, 0.25, 0.5 }, cxcywh.Select(item => (double)item).ToArray());
        }

        // ---- unknown keys ----

        [Test]
        public void Write_PutsUnknownKeysAfterTheKnownOnes_InTheirOrder_AndNeverOverwritesAKnownKey()
        {
            var extras = Models.Full();
            extras.Unknown["zeta"] = 1;
            extras.Unknown["alpha"] = new JObject { ["deep"] = new JArray(1, 2) };
            extras.Unknown["frames"] = "not the frames";
            extras.Unknown["schema_version"] = 99;
            extras.Objects[0].Unknown["note"] = "n";
            extras.Objects[0].Unknown["id"] = "other";
            extras.Objects[0].Label.Unknown["lang"] = "en";
            extras.Space.Unknown["site"] = "s";
            extras.Planes[0].Pose.Unknown["frame"] = "local-floor";

            var written = ExtrasWriter.Write(extras);

            AssertKeyOrder(written, "schema", "schema_version", "units", "space", "consent", "device", "frames", "objects", "planes", "bounds", "zeta", "alpha");
            Assert.AreEqual(JTokenType.Array, written["frames"].Type);
            Assert.AreEqual(2, (int)written["schema_version"]);
            Assert.AreEqual(2, ((JArray)written["alpha"]["deep"]).Count);
            Assert.AreEqual("s1-track-1", (string)written["objects"][0]["id"]);
            Assert.AreEqual("n", (string)written["objects"][0]["note"]);
            AssertKeyOrder(written["objects"][0], "id", "track_id", "label", "confidence", "source", "pose", "normal", "observations", "note");
            Assert.AreEqual("en", (string)written["objects"][0]["label"]["lang"]);
            Assert.AreEqual("s", (string)written["space"]["site"]);
            Assert.AreEqual("local-floor", (string)written["planes"][0]["pose"]["frame"]);
        }

        // An unknown key under the name of a part would be read back as that part, in the wrong type: it is not written.
        [Test]
        public void Write_NeverWritesAnUnknownKeyUnderTheNameOfAPart_EvenWhenThePartIsLeftOut()
        {
            var extras = new ScanExtras();
            extras.Unknown["consent"] = "x";
            extras.Unknown["device"] = 5;
            extras.Unknown["bounds"] = "wide";
            var obj = new ScanObject { Id = "o" };
            obj.Unknown["normal"] = "up";
            obj.Unknown["bounds"] = 1;
            obj.Unknown["anchor_uuid"] = 5;
            obj.Unknown["fine"] = "kept";
            extras.Objects.Add(obj);
            var frame = new ScanFrame { Id = "f" };
            frame.Unknown["pose"] = "here";
            frame.Unknown["intrinsics"] = 3;
            extras.Frames.Add(frame);
            var plane = new ScanPlane { Id = "p" };
            plane.Unknown["center"] = 1;
            plane.Unknown["size"] = 2;
            extras.Planes.Add(plane);

            var written = ExtrasWriter.Write(extras);

            AssertKeyOrder(written, "schema", "schema_version", "units", "space", "frames", "objects", "planes");
            AssertKeyOrder(written["objects"][0], "id", "track_id", "label", "confidence", "source", "pose", "observations", "fine");
            AssertKeyOrder(written["frames"][0], "id");
            AssertKeyOrder(written["planes"][0], "id", "label", "semantic", "pose", "boundary");
            var errors = MiniSchema.Validate(written, JsonAssert.ParseObject(TestPaths.ReadSchema()));
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [Test]
        public void Write_ANegativeTrackId_IsWrittenAsZero()
        {
            var extras = new ScanExtras();
            extras.Objects.Add(new ScanObject { Id = "o", TrackId = -9 });

            Assert.AreEqual(0, (int)ExtrasWriter.Write(extras)["objects"][0]["track_id"]);
        }

        [Test]
        public void Write_CopiesUnknownValues_SoTheModelAndTheDocumentAreIndependent()
        {
            var extras = new ScanExtras();
            extras.Unknown["list"] = new JArray(1, 2, 3);

            var written = ExtrasWriter.Write(extras);
            ((JArray)written["list"]).Add(4);

            Assert.AreEqual(3, ((JArray)extras.Unknown["list"]).Count);
        }

        // ---- what a model can hold that a document cannot ----

        [Test]
        public void Write_ReplacesNumbersThatAreNotFiniteWithZero()
        {
            var extras = new ScanExtras();
            var obj = new ScanObject
            {
                Id = "o",
                Confidence = double.NaN,
                Pose = new ScanPose(new Vec3(double.NaN, double.PositiveInfinity, 1), new Quat(0, 0, double.NegativeInfinity, 1)),
                Normal = new Vec3(double.NaN, 0, 0),
                Bounds = new ScanBounds(new Vec3(double.PositiveInfinity, 0, 0), new Vec3(1, double.NaN, 1))
            };
            obj.Observations.Add(new ScanObservation { FrameId = "f", Score = double.NaN });
            extras.Objects.Add(obj);
            extras.Frames.Add(new ScanFrame { Id = "f", Intrinsics = new ScanIntrinsics { Fx = double.NaN, Fy = 1, Cx = double.PositiveInfinity, Cy = 2 } });
            var plane = new ScanPlane { Id = "p", Size = new Vec2(double.NaN, 1), Center = new Vec2(0, double.PositiveInfinity) };
            plane.Boundary.Add(new Vec3(double.NaN, 1, 1));
            extras.Planes.Add(plane);
            extras.Bounds = new ScanBounds(new Vec3(double.NaN, 0, 0), new Vec3(1, 1, 1));

            string text = null;
            Assert.DoesNotThrow(() => text = ExtrasWriter.ToJson(extras));

            StringAssert.DoesNotContain("NaN", text);
            StringAssert.DoesNotContain("Infinity", text);
            var written = JsonAssert.ParseObject(text);
            Assert.AreEqual(0.0, (double)written["objects"][0]["pose"]["position"]["x"]);
            Assert.AreEqual(0.0, (double)written["objects"][0]["pose"]["position"]["y"]);
            Assert.AreEqual(1.0, (double)written["objects"][0]["pose"]["position"]["z"]);
            Assert.AreEqual(0.0, (double)written["objects"][0]["confidence"]);
            Assert.AreEqual(0.0, (double)written["frames"][0]["intrinsics"]["fx"]);
            Assert.AreEqual(0.0, (double)written["planes"][0]["size"]["x"]);
        }

        [Test]
        public void Write_ClampsConfidenceAndScore_AndGivesAnInvalidMetaLabelOther()
        {
            var extras = new ScanExtras();
            var high = new ScanObject { Id = "high", Confidence = 4.5, Label = new ScanLabel("x", "x", "CHAIR") };
            high.Observations.Add(new ScanObservation { FrameId = "f", Score = -0.5 });
            var low = new ScanObject { Id = "low", Confidence = -1, Label = new ScanLabel("x", "x", null) };
            extras.Objects.Add(high);
            extras.Objects.Add(low);
            extras.Planes.Add(new ScanPlane { Id = "p", Semantic = "floor" });

            var written = ExtrasWriter.Write(extras);

            Assert.AreEqual(1.0, (double)written["objects"][0]["confidence"]);
            Assert.AreEqual(0.0, (double)written["objects"][0]["observations"][0]["score"]);
            Assert.AreEqual("OTHER", (string)written["objects"][0]["label"]["semantic"]);
            Assert.AreEqual(0.0, (double)written["objects"][1]["confidence"]);
            Assert.AreEqual("OTHER", (string)written["objects"][1]["label"]["semantic"]);
            Assert.AreEqual("OTHER", (string)written["planes"][0]["semantic"]);
        }

        [Test]
        public void Write_SkipsNullEntries_AndFillsAMissingSpaceLabelAndPose()
        {
            var extras = new ScanExtras { Space = null };
            extras.Objects.Add(null);
            extras.Objects.Add(new ScanObject { Id = "o", Label = null, Pose = null });
            extras.Frames.Add(null);
            extras.Planes.Add(null);
            extras.Planes.Add(new ScanPlane { Id = "p", Pose = null });
            extras.Objects[1].Observations.Add(null);

            var written = ExtrasWriter.Write(extras);

            Assert.AreEqual("default", (string)written["space"]["id"]);
            Assert.AreEqual(1, written["objects"].Count());
            Assert.AreEqual(0, written["frames"].Count());
            Assert.AreEqual(1, written["planes"].Count());
            Assert.AreEqual(string.Empty, (string)written["objects"][0]["label"]["text"]);
            Assert.AreEqual(0.0, (double)written["objects"][0]["pose"]["position"]["x"]);
            Assert.AreEqual(0, written["objects"][0]["observations"].Count());
            Assert.AreEqual(1.0, (double)written["objects"][0]["pose"]["quaternion"]["w"], "a missing pose is the origin with no rotation");
        }

        [Test]
        public void Write_ANullModel_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => ExtrasWriter.Write(null));
            Assert.Throws<ArgumentNullException>(() => ExtrasWriter.ToJson(null));
        }

        [Test]
        public void ToJson_IsIndentedByDefault_AndCompactOnRequest_AndBothParseBack()
        {
            var model = Models.Full();

            string indented = ExtrasWriter.ToJson(model);
            string compact = ExtrasWriter.ToJson(model, false);

            StringAssert.Contains("\n  \"schema\": \"companion.scan-extras\"", indented);
            StringAssert.DoesNotContain("\r", indented, "line feeds only, so the text is the same on every machine");
            StringAssert.DoesNotContain("\n", compact);
            Assert.IsTrue(JToken.DeepEquals(JsonAssert.Parse(indented), JsonAssert.Parse(compact)));
            Assert.IsTrue(JToken.DeepEquals(ExtrasWriter.Write(model), JsonAssert.Parse(compact)));
            Assert.AreEqual('{', indented[0], "no byte order mark");
        }

        [Test]
        public void Write_DoesNotChangeTheModel()
        {
            var model = Models.Full();
            model.Unknown["extra"] = 1;
            string before = ExtrasWriter.ToJson(model);

            ExtrasWriter.Write(model);

            Assert.AreEqual(before, ExtrasWriter.ToJson(model));
        }
    }
}
