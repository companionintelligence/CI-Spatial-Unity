using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CompanionScan.Export;
using CompanionScan.Mapping;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// Characterization tests for the extras that <see cref="SpatialMapExporter"/> writes into the
    /// root of the glTF. They call the exporter's private methods by reflection, with no headset
    /// and no mesh export: BuildExtras needs only the pin manager, the snapshot store and the
    /// floor height. The golden file Golden/extras-v1.json holds the extras for two pins and one
    /// snapshot. A test with "Currently" in its name records a result that looks doubtful, not a
    /// requirement.
    /// </summary>
    public class SpatialMapExporterExtrasCharacterizationTests
    {
        private const string GoldenFile = "CompanionScan/Tests/Editor/Characterization/Golden/extras-v1.json";
        private const string TimestampPlaceholder = "<timestamp_utc>";
        private const double Tolerance = 1e-4;

        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
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
            foreach (var created in _created)
            {
                if (created != null)
                {
                    UnityEngine.Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        // ---- BuildExtras ----

        [Test]
        public void BuildExtras_ForTwoPinsAndOneSnapshot_MatchesTheGoldenFile()
        {
            var exporter = NewExporter(out var pins, out var snapshots);
            pins.AddPin(ChairPin());
            pins.AddPin(TablePin());
            snapshots.AddSnapshot(SnapshotCapture(), 2f, NewCamera());

            var extras = BuildExtras(exporter, null, BuildSnapshotExtras(exporter), "room-1");

            // What GltfExtrasWriter writes: the extras as indented text.
            var written = (JObject)ParseText(extras.ToString());
            StringAssert.IsMatch(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$", (string)written["timestamp_utc"],
                "timestamp_utc is the current UTC time in the round-trip format");
            written["timestamp_utc"] = TimestampPlaceholder;

            // The top level repeats five members of cv_mapping, as copies. The golden file lists them once.
            var cvMapping = (JObject)written["cv_mapping"];
            foreach (var key in new[] { "bounds", "boundary", "floorplan", "pins", "snapshots" })
            {
                Assert.IsTrue(JToken.DeepEquals(cvMapping[key], written[key]), $"the top-level \"{key}\" is not the same as cv_mapping.{key}");
                written.Remove(key);
            }

            var golden = (JObject)ParseText(File.ReadAllText(Path.Combine(Application.dataPath, GoldenFile)));
            AssertSameJson(golden, written, "the extras differ from Golden/extras-v1.json");
        }

        [Test]
        public void BuildExtras_WithNoPinManagerNoSnapshotsAndNoSpace_HasEmptyListsAndAnEmptyBounds()
        {
            var exporter = NewExporter(out _, out _, withPins: false, withSnapshots: false);

            var extras = BuildExtras(exporter, null, null, null);

            AssertSameJson(JObject.Parse(@"{
                'schema_version': '1.0', 'timestamp_utc': '" + TimestampPlaceholder + @"', 'units': 'meters',
                'meta_scene': { 'available': false },
                'bounds': { 'center': [0, 0, 0], 'size': [0, 0, 0] }, 'boundary': [], 'floorplan': [], 'pins': [],
                'cv_mapping': { 'bounds': { 'center': [0, 0, 0], 'size': [0, 0, 0] }, 'boundary': [], 'floorplan': [], 'pins': [] }
            }"), WithPlaceholderTime(extras), "extras with nothing in them");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void BuildExtras_WithoutASpaceId_LeavesSpaceIdOut(string spaceId)
        {
            var extras = BuildExtras(NewExporter(out _, out _), null, null, spaceId);

            Assert.IsNull(extras["space_id"]);
            Assert.IsNull(extras["cv_mapping"]["space_id"]);
        }

        [Test]
        public void BuildExtras_WithASpaceId_WritesItAtTheTopAndInCvMapping()
        {
            var extras = BuildExtras(NewExporter(out _, out _), null, null, "room-1");

            Assert.AreEqual("room-1", (string)extras["space_id"]);
            Assert.AreEqual("room-1", (string)extras["cv_mapping"]["space_id"]);
        }

        [Test]
        public void BuildExtras_ListsThePinsInTheOrderOfThePinManager_AndTheFloorPlanIsTheirHullAtFloorY()
        {
            var exporter = NewExporter(out var pins, out _);
            SetField(exporter, "floorY", 0.25f);
            pins.AddPin(new SpatialPin { label = "a", trackId = 1, position = new Vector3(0f, 0.9f, 0f) });
            pins.AddPin(new SpatialPin { label = "b", trackId = 2, position = new Vector3(4f, 0.1f, 0f) });
            pins.AddPin(new SpatialPin { label = "c", trackId = 3, position = new Vector3(0f, 0.5f, 3f) });

            var extras = BuildExtras(exporter, null, null, null);

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, extras["pins"].Select(pin => (string)pin["label"]).ToArray());
            AssertSameJson(JToken.Parse("[ [0, 0.25, 0], [4, 0.25, 0], [0, 0.25, 3] ]"), extras["floorplan"], "floorplan");
            AssertSameJson(JToken.Parse("{ 'center': [2, 0.5, 1.5], 'size': [4, 0.8, 3] }"), extras["bounds"], "bounds use the pins' own heights");
        }

        // With fewer than three pins the extras say "floorplan": [] (and no floor mesh is made).
        [Test]
        public void BuildExtras_WithFewerThanThreePins_HasAnEmptyFloorPlan()
        {
            var exporter = NewExporter(out var pins, out _);
            pins.AddPin(ChairPin());
            pins.AddPin(TablePin());

            var extras = BuildExtras(exporter, null, null, null);

            Assert.AreEqual(0, ((JArray)extras["floorplan"]).Count);
        }

        [Test]
        public void BuildExtras_WithASpaceSetupSnapshot_WritesItsAnchorsRoomsAndLabels()
        {
            var meta = new MetaSceneSnapshot
            {
                timestamp = 3.5,
                primaryRoomUuid = "room-1",
                roomUuids = new List<string> { "room-1" },
                supportedLabels = new List<string> { "TABLE", "OTHER" },
                anchors = new List<MetaSceneAnchorSnapshot>
                {
                    new MetaSceneAnchorSnapshot
                    {
                        uuid = "anchor-1",
                        labels = new List<string> { "TABLE" },
                        position = new Vector3(1f, 0.5f, 2f),
                        rotation = new Quaternion(0.125f, 0.25f, 0.375f, 0.5f),
                        hasPlane = true,
                        planeCenter = new Vector3(1f, 0.5f, 2f),
                        planeSize = new Vector3(1.5f, 0.75f, 0f),
                        planeBoundary = new List<Vector3> { Vector3.zero, Vector3.right, new Vector3(1f, 0f, 1f) },
                        hasVolume = false,
                        hasMesh = true,
                        meshVertexCount = 100,
                        meshTriangleCount = 50
                    }
                }
            };

            var extras = BuildExtras(NewExporter(out _, out _), meta, null, "room-1");

            AssertSameJson(JToken.Parse(@"{
                'available': true, 'error': null, 'timestamp': 3.5,
                'supported_labels': [ 'TABLE', 'OTHER' ], 'room_uuids': [ 'room-1' ], 'primary_room_uuid': 'room-1',
                'anchors': [ {
                    'uuid': 'anchor-1', 'labels': [ 'TABLE' ],
                    'position': [ 1, 0.5, 2 ], 'rotation': [ 0.125, 0.25, 0.375, 0.5 ],
                    'has_plane': true, 'plane_center': [ 1, 0.5, 2 ], 'plane_size': [ 1.5, 0.75, 0 ],
                    'plane_boundary': [ [ 0, 0, 0 ], [ 1, 0, 0 ], [ 1, 0, 1 ] ],
                    'has_volume': false, 'volume_center': [ 0, 0, 0 ], 'volume_size': [ 0, 0, 0 ],
                    'has_mesh': true, 'mesh_vertex_count': 100, 'mesh_triangle_count': 50
                } ]
            }"), extras["meta_scene"], "meta_scene");
        }

        [Test]
        public void BuildExtras_WithAFailedSpaceSetupRead_SaysTheSceneIsNotAvailableAndWhy()
        {
            var meta = new MetaSceneSnapshot { timestamp = 1.5, error = "No scene model found. Run Space Setup on-device." };

            var extras = BuildExtras(NewExporter(out _, out _), meta, null, null);

            AssertSameJson(JToken.Parse(@"{
                'available': false, 'error': 'No scene model found. Run Space Setup on-device.', 'timestamp': 1.5,
                'supported_labels': [], 'room_uuids': [], 'primary_room_uuid': '', 'anchors': []
            }"), extras["meta_scene"], "meta_scene");
        }

        // ---- BuildSnapshotExtras ----

        [Test]
        public void BuildSnapshotExtras_WritesEachImageBesideTheExtras_AndEmptiesTheStore()
        {
            var exporter = NewExporter(out _, out var snapshots);
            var capture = SnapshotCapture();
            snapshots.AddSnapshot(capture, 2f, null);

            var array = BuildSnapshotExtras(exporter);

            Assert.AreEqual(1, array.Count);
            Assert.AreEqual("snapshots/snap_0001.jpg", (string)array[0]["image"], "the path is relative and uses forward slashes");
            CollectionAssert.AreEqual(capture.jpegBytes, File.ReadAllBytes(Path.Combine(_directory, "snapshots", "snap_0001.jpg")));
            Assert.AreEqual(0, snapshots.Frames.Count, "the exporter takes the frames out of the store");
        }

        [Test]
        public void BuildSnapshotExtras_ReturnsNull_WithoutAStoreOrWithoutFrames_AndMakesNoFolder()
        {
            var withoutStore = NewExporter(out _, out _, withSnapshots: false);
            var emptyStore = NewExporter(out _, out _);

            Assert.IsNull(BuildSnapshotExtras(withoutStore));
            Assert.IsNull(BuildSnapshotExtras(emptyStore));
            Assert.IsFalse(Directory.Exists(Path.Combine(_directory, "snapshots")));
        }

        [Test]
        public void BuildExtras_LeavesSnapshotsOutWhenThereAreNone_AndWritesThemAtBothLevelsWhenThereAre()
        {
            var exporter = NewExporter(out _, out _);

            var without = BuildExtras(exporter, null, null, null);
            var with = BuildExtras(exporter, null, new JArray(new JObject { ["id"] = "snap_0001" }), null);

            Assert.IsNull(without["snapshots"]);
            Assert.IsNull(without["cv_mapping"]["snapshots"]);
            Assert.AreEqual("snap_0001", (string)with["snapshots"][0]["id"]);
            Assert.AreEqual("snap_0001", (string)with["cv_mapping"]["snapshots"][0]["id"]);
        }

        // ---- MergeExtras: the extras of an earlier export of the same space are kept ----

        [Test]
        public void MergeExtras_WithNoEarlierExtras_ReturnsTheNewOnesThemselves_AndWithNoNewOnesTheEarlierOnes()
        {
            var earlier = new JObject { ["units"] = "meters" };
            var current = new JObject { ["units"] = "meters", ["pins"] = new JArray() };

            Assert.AreSame(current, MergeExtras(null, current));
            Assert.AreSame(earlier, MergeExtras(earlier, null));
        }

        // A re-export takes the new time, bounds and scene; a pin of the same object (same session and track) replaces its
        // earlier entry instead of being listed twice, and pins and snapshots only the earlier export had are kept.
        [Test]
        public void MergeExtras_ReplacesAPinOfTheSameObject_KeepsTheRest_AndTakesTheNewTimeAndBounds()
        {
            var earlier = JObject.Parse(@"{
                'schema_version': '1.0', 'timestamp_utc': 'old', 'units': 'meters', 'custom': 'kept',
                'bounds': { 'center': [0, 0, 0] }, 'boundary': [], 'floorplan': [],
                'pins': [ { 'label': 'chair', 'capture_session_id': 's1', 'track_id': 1, 'position': [0, 0, 1] },
                          { 'label': 'bin', 'capture_session_id': 's1', 'track_id': 2, 'position': [4, 0, 4] } ],
                'snapshots': [ { 'id': 'snap_0001' } ],
                'space_id': 'room-old', 'meta_scene': { 'available': false },
                'cv_mapping': { 'bounds': { 'center': [0, 0, 0] },
                                'pins': [ { 'label': 'chair', 'capture_session_id': 's1', 'track_id': 1, 'position': [0, 0, 1] },
                                          { 'label': 'bin', 'capture_session_id': 's1', 'track_id': 2, 'position': [4, 0, 4] } ],
                                'snapshots': [ { 'id': 'snap_0001' } ], 'space_id': 'room-old' }
            }");
            var current = JObject.Parse(@"{
                'schema_version': '1.0', 'timestamp_utc': 'new', 'units': 'meters',
                'bounds': { 'center': [1, 1, 1] }, 'boundary': [], 'floorplan': [],
                'pins': [ { 'label': 'chair', 'capture_session_id': 's1', 'track_id': 1, 'position': [0, 0, 2] },
                          { 'label': 'lamp', 'capture_session_id': 's1', 'track_id': 3, 'position': [1, 1, 1] } ],
                'snapshots': [ { 'id': 'snap_0002' } ],
                'space_id': 'room-new', 'meta_scene': { 'available': true },
                'cv_mapping': { 'bounds': { 'center': [1, 1, 1] },
                                'pins': [ { 'label': 'chair', 'capture_session_id': 's1', 'track_id': 1, 'position': [0, 0, 2] },
                                          { 'label': 'lamp', 'capture_session_id': 's1', 'track_id': 3, 'position': [1, 1, 1] } ],
                                'snapshots': [ { 'id': 'snap_0002' } ], 'space_id': 'room-new' }
            }");
            var mergedPins = @"[ { 'label': 'chair', 'capture_session_id': 's1', 'track_id': 1, 'position': [0, 0, 2] },
                                 { 'label': 'bin', 'capture_session_id': 's1', 'track_id': 2, 'position': [4, 0, 4] },
                                 { 'label': 'lamp', 'capture_session_id': 's1', 'track_id': 3, 'position': [1, 1, 1] } ]";

            var merged = MergeExtras(earlier, current);

            AssertSameJson(JObject.Parse(@"{
                'schema_version': '1.0', 'timestamp_utc': 'new', 'units': 'meters', 'custom': 'kept',
                'bounds': { 'center': [1, 1, 1] }, 'boundary': [], 'floorplan': [],
                'pins': " + mergedPins + @",
                'snapshots': [ { 'id': 'snap_0001' }, { 'id': 'snap_0002' } ],
                'space_id': 'room-new', 'meta_scene': { 'available': true },
                'cv_mapping': { 'bounds': { 'center': [1, 1, 1] },
                                'pins': " + mergedPins + @",
                                'snapshots': [ { 'id': 'snap_0001' }, { 'id': 'snap_0002' } ], 'space_id': 'room-new' }
            }"), merged, "merged extras");
        }

        // The same export merged twice changes nothing the second time: the file does not grow with every save.
        [Test]
        public void MergeExtras_OfTheSameExportAgain_ChangesNothing()
        {
            var export = JObject.Parse(@"{
                'schema_version': '1.0', 'timestamp_utc': 't', 'units': 'meters', 'bounds': {}, 'boundary': [], 'floorplan': [],
                'pins': [ { 'label': 'chair', 'capture_session_id': 's1', 'track_id': 1 },
                          { 'label': 'TABLE', 'anchor_uuid': 'anchor-1' }, { 'label': 'cup', 'capture_id': 'snap_0001', 'position': [1, 2, 3] } ],
                'snapshots': [ { 'id': 'snap_0001' } ], 'space_id': 'room', 'meta_scene': { 'available': false },
                'cv_mapping': { 'pins': [ { 'label': 'chair', 'capture_session_id': 's1', 'track_id': 1 },
                                          { 'label': 'TABLE', 'anchor_uuid': 'anchor-1' }, { 'label': 'cup', 'capture_id': 'snap_0001', 'position': [1, 2, 3] } ],
                                'snapshots': [ { 'id': 'snap_0001' } ] }
            }");

            var once = MergeExtras(JObject.Parse(export.ToString()), JObject.Parse(export.ToString()));
            var twice = MergeExtras(JObject.Parse(once.ToString()), JObject.Parse(export.ToString()));

            AssertSameJson(export, once, "once");
            AssertSameJson(once, twice, "twice");
            Assert.AreEqual(3, ((JArray)twice["pins"]).Count);
        }

        // A file written by the old exporter lists every pin twice; the next export collapses them.
        [Test]
        public void MergeExtras_CollapsesPinsThatAnEarlierExportListedTwice()
        {
            var earlier = JObject.Parse(@"{ 'pins': [ { 'label': 'chair', 'capture_session_id': 's1', 'track_id': 1 },
                                                    { 'label': 'chair', 'capture_session_id': 's1', 'track_id': 1 } ] }");
            var current = JObject.Parse("{ 'pins': [], 'cv_mapping': { 'pins': [] } }");

            var merged = MergeExtras(earlier, current);

            Assert.AreEqual(1, ((JArray)merged["pins"]).Count);
        }

        // Track numbers start again at 1 in every session, so the session is part of what makes a pin the same object.
        [Test]
        public void MergeExtras_KeepsAnEarlierSessionsPin_EvenWithTheSameTrackNumber()
        {
            var earlier = JObject.Parse("{ 'pins': [ { 'label': 'chair', 'capture_session_id': 's0', 'track_id': 1 } ] }");
            var current = JObject.Parse(@"{ 'pins': [ { 'label': 'lamp', 'capture_session_id': 's1', 'track_id': 1 } ],
                                            'cv_mapping': { 'pins': [ { 'label': 'lamp', 'capture_session_id': 's1', 'track_id': 1 } ] } }");

            var merged = MergeExtras(earlier, current);

            CollectionAssert.AreEqual(new[] { "chair", "lamp" }, ((JArray)merged["pins"]).Select(pin => (string)pin["label"]).ToArray());
        }

        // A Space Setup anchor is the same object in every session: its newer entry replaces the older.
        [Test]
        public void MergeExtras_ReplacesASceneAnchorsPin_WhateverTheSession()
        {
            var earlier = JObject.Parse("{ 'pins': [ { 'label': 'TABLE', 'anchor_uuid': 'a-1', 'position': [0, 0, 0] } ] }");
            var current = JObject.Parse(@"{ 'pins': [ { 'label': 'TABLE', 'anchor_uuid': 'a-1', 'position': [0, 0, 1] } ],
                                            'cv_mapping': { 'pins': [ { 'label': 'TABLE', 'anchor_uuid': 'a-1', 'position': [0, 0, 1] } ] } }");

            var merged = MergeExtras(earlier, current);

            var pins = (JArray)merged["pins"];
            Assert.AreEqual(1, pins.Count);
            Assert.AreEqual(1f, (float)pins[0]["position"][2]);
        }

        // Snapshots are the same by id.
        [Test]
        public void MergeExtras_ReplacesASnapshotWithTheSameId()
        {
            var earlier = JObject.Parse("{ 'snapshots': [ { 'id': 'snap_0001', 'image': 'old.jpg' }, { 'id': 'snap_0002' } ] }");
            var current = JObject.Parse(@"{ 'pins': [], 'snapshots': [ { 'id': 'snap_0001', 'image': 'new.jpg' } ],
                                            'cv_mapping': { 'pins': [], 'snapshots': [ { 'id': 'snap_0001', 'image': 'new.jpg' } ] } }");

            var merged = MergeExtras(earlier, current);

            var snapshots = (JArray)merged["snapshots"];
            CollectionAssert.AreEqual(new[] { "snap_0001", "snap_0002" }, snapshots.Select(s => (string)s["id"]).ToArray());
            Assert.AreEqual("new.jpg", (string)snapshots[0]["image"]);
        }

        // With no pins on either side the pins are an empty list and there is no snapshots member, never JSON null.
        [Test]
        public void MergeExtras_WritesAnEmptyPinListAndNoSnapshotsMember_WhenNeitherSideHasAny()
        {
            var earlier = JObject.Parse("{ 'units': 'meters' }");
            var current = JObject.Parse("{ 'units': 'meters', 'pins': [], 'cv_mapping': { 'pins': [] } }");

            var merged = MergeExtras(earlier, current);

            Assert.AreEqual(JTokenType.Array, merged["pins"].Type);
            Assert.AreEqual(0, ((JArray)merged["pins"]).Count);
            Assert.IsNull(merged["snapshots"], "no snapshots member");
            Assert.AreEqual(0, ((JArray)merged["cv_mapping"]["pins"]).Count);
            Assert.IsNull(merged["cv_mapping"]["snapshots"]);
        }

        // ---- ReadExistingExtras ----

        [Test]
        public void ReadExistingExtras_ReturnsNullForAMissingFileAndForAFileWithoutExtras()
        {
            var withoutExtras = Path.Combine(_directory, "plain.gltf");
            File.WriteAllText(withoutExtras, "{ \"asset\": { \"version\": \"2.0\" } }");
            var extrasNotAnObject = Path.Combine(_directory, "number.gltf");
            File.WriteAllText(extrasNotAnObject, "{ \"extras\": 5 }");

            Assert.IsNull(ReadExistingExtras(Path.Combine(_directory, "missing.gltf")));
            Assert.IsNull(ReadExistingExtras(null));
            Assert.IsNull(ReadExistingExtras(string.Empty));
            Assert.IsNull(ReadExistingExtras(withoutExtras));
            Assert.IsNull(ReadExistingExtras(extrasNotAnObject));
        }

        [Test]
        public void ReadExistingExtras_ReturnsTheExtrasObjectOfTheFile()
        {
            var path = Path.Combine(_directory, "model.gltf");
            File.WriteAllText(path, "{ \"asset\": { \"version\": \"2.0\" }, \"extras\": { \"units\": \"meters\" } }");

            var extras = ReadExistingExtras(path);

            Assert.AreEqual("meters", (string)extras["units"]);
        }

        // A glTF that is not valid JSON (an export cut short) used to make every later export of that space throw before it
        // wrote anything. Its extras cannot be kept, so the next export starts them afresh, with a warning.
        [Test]
        public void ReadExistingExtras_IgnoresAFileThatIsNotJson_WithAWarning()
        {
            var path = Path.Combine(_directory, "broken.gltf");
            File.WriteAllText(path, "{ \"asset\": ");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Ignoring the extras of .*broken\\.gltf"));

            Assert.IsNull(ReadExistingExtras(path));
        }

        // ---- the name and folder of the export ----

        [Test]
        public void ResolveSpaceId_UsesThePrimaryRoomThenTheFirstRoomThenTheFirstAnchorThenDefault()
        {
            Assert.AreEqual("default", ResolveSpaceId(null, null));
            Assert.AreEqual("room-A", ResolveSpaceId(Snapshot(primary: "room-A", rooms: new[] { "room-B" }, anchors: new[] { "anchor-C" }), null));
            Assert.AreEqual("room-B", ResolveSpaceId(Snapshot(primary: "  ", rooms: new[] { "room-B", "room-E" }, anchors: new[] { "anchor-C" }), null));
            Assert.AreEqual("anchor-C", ResolveSpaceId(Snapshot(primary: null, rooms: new string[0], anchors: new[] { "anchor-C" }), null));
            Assert.AreEqual("default", ResolveSpaceId(Snapshot(primary: null, rooms: new string[0], anchors: new string[0]), null));
        }

        [Test]
        public void ResolveSpaceId_UsesTheLastSnapshotOnlyWhenThereIsNoNewOne()
        {
            var last = Snapshot(primary: "room-last", rooms: new string[0], anchors: new string[0]);
            var emptyNew = Snapshot(primary: null, rooms: new string[0], anchors: new string[0]);
            var newer = Snapshot(primary: "room-new", rooms: new string[0], anchors: new string[0]);

            Assert.AreEqual("room-last", ResolveSpaceId(null, last));
            Assert.AreEqual("room-new", ResolveSpaceId(newer, last));
            Assert.AreEqual("default", ResolveSpaceId(emptyNew, last), "a new snapshot with no room is used, not replaced by the last one");
        }

        [TestCase("room-1", "room-1")]
        [TestCase(null, "default")]
        [TestCase("", "default")]
        [TestCase("   ", "default")]
        [TestCase("a/b", "a-b")]
        [TestCase("1c3f9a52-0000-4000-8000-000000000001", "1c3f9a52-0000-4000-8000-000000000001")]
        public void SanitizeFileToken_KeepsTheTokenExceptForTheCharactersAFileNameCannotHave(string token, string expected)
        {
            Assert.AreEqual(expected, SanitizeFileToken(token));
        }

        [Test]
        public void SanitizeFileToken_ReplacesEveryCharacterThatIsInvalidInAFileNameOnThisPlatformWithAHyphen()
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                Assert.AreEqual("a-b", SanitizeFileToken("a" + invalid + "b"), "for U+" + ((int)invalid).ToString("X4"));
            }
        }

        [Test]
        public void BuildExportName_IsTheFileNameAndTheSpaceToken_OrTheDefaultFileName()
        {
            var exporter = NewExporter(out _, out _);

            Assert.AreEqual("spatial-map_room-1", BuildExportName(exporter, "room-1"));
            Assert.AreEqual("spatial-map", BuildExportName(exporter, "  "));

            SetField(exporter, "fileName", "scan");
            Assert.AreEqual("scan_room-1", BuildExportName(exporter, "room-1"));
            SetField(exporter, "fileName", "   ");
            Assert.AreEqual("spatial-map_room-1", BuildExportName(exporter, "room-1"));
        }

        // ---- test data ----

        private static SpatialPin ChairPin()
        {
            return new SpatialPin
            {
                label = "chair",
                semanticLabel = "OTHER",
                score = 0.5f,
                source = PinSource.CvServer,
                position = new Vector3(1f, 0.5f, 2f),
                normal = new Vector3(0f, 1f, 0f),
                rotation = new Quaternion(0.125f, 0.25f, 0.375f, 0.5f),
                bboxPixels = new SerializableRect(10f, 20f, 30f, 40f),
                bboxNormalized = new SerializableRect(0.0625f, 0.125f, 0.25f, 0.5f),
                sourcePrompt = "chair . table .",
                timestamp = 12.5,
                captureId = "snap_0001",
                captureSessionId = "session-1",
                captureFrameIndex = 3,
                placementConfidence = 0.75f,
                hitType = RaycastHitType.Physics,
                trackId = 7
            };
        }

        // Without a prompt or capture ids, to record how a missing string is written: "prompt" is
        // null, "anchor_uuid", "capture_id" and "capture_session_id" are "".
        private static SpatialPin TablePin()
        {
            return new SpatialPin
            {
                label = "TABLE",
                semanticLabel = "TABLE",
                score = 1f,
                source = PinSource.MetaScene,
                position = new Vector3(0f, 1.5f, 4f),
                normal = Vector3.up,
                planeSize = new Vector3(1.5f, 0.75f, 0f),
                volumeSize = new Vector3(1.5f, 0.75f, 0.5f),
                anchorUuid = "anchor-1"
            };
        }

        // Every element of the intrinsics is different, so that a change of order shows.
        private static CaptureFrame SnapshotCapture()
        {
            var intrinsics = Matrix4x4.zero;
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    intrinsics[row, column] = 10 * row + column + 1;
                }
            }

            return new CaptureFrame
            {
                timestamp = 20.5,
                jpegBytes = new byte[] { 1, 2, 3, 4 },
                imageWidth = 640,
                imageHeight = 480,
                cameraPose = new Pose(new Vector3(0.5f, 1.5f, -1f), new Quaternion(0.125f, 0.25f, 0.375f, 0.5f)),
                cameraIntrinsics = intrinsics
            };
        }

        private static MetaSceneSnapshot Snapshot(string primary, string[] rooms, string[] anchors)
        {
            return new MetaSceneSnapshot
            {
                primaryRoomUuid = primary,
                roomUuids = rooms.ToList(),
                anchors = anchors.Select(uuid => new MetaSceneAnchorSnapshot { uuid = uuid }).ToList()
            };
        }

        private SpatialMapExporter NewExporter(out SpatialPinManager pins, out SnapshotFrameStore snapshots, bool withPins = true, bool withSnapshots = true)
        {
            var go = new GameObject("Exporter");
            _created.Add(go);
            var exporter = go.AddComponent<SpatialMapExporter>();
            pins = null;
            snapshots = null;
            if (withPins)
            {
                pins = go.AddComponent<SpatialPinManager>();
                SetField(exporter, "pinManager", pins);
            }

            if (withSnapshots)
            {
                snapshots = go.AddComponent<SnapshotFrameStore>();
                SetField(exporter, "snapshotStore", snapshots);
            }

            return exporter;
        }

        // A camera at the origin looking along +Z, 90 degrees high and twice as wide as high.
        private Camera NewCamera()
        {
            var go = new GameObject("SnapshotCamera");
            _created.Add(go);
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 90f;
            camera.aspect = 2f;
            return camera;
        }

        // ---- calls to the exporter's private methods ----

        private static JObject BuildExtras(SpatialMapExporter exporter, MetaSceneSnapshot meta, JArray snapshotExtras, string spaceId)
        {
            return (JObject)Call(exporter, "BuildExtras", exporter.transform, meta, snapshotExtras, spaceId);
        }

        private JArray BuildSnapshotExtras(SpatialMapExporter exporter)
        {
            return (JArray)Call(exporter, "BuildSnapshotExtras", _directory);
        }

        private static JObject MergeExtras(JObject existing, JObject current)
        {
            return (JObject)Call(null, "MergeExtras", existing, current);
        }

        private static JObject ReadExistingExtras(string gltfPath)
        {
            return (JObject)Call(null, "ReadExistingExtras", gltfPath);
        }

        private static string ResolveSpaceId(MetaSceneSnapshot current, MetaSceneSnapshot fallback)
        {
            return (string)Call(null, "ResolveSpaceId", current, fallback);
        }

        private static string SanitizeFileToken(string token)
        {
            return (string)Call(null, "SanitizeFileToken", token);
        }

        private static string BuildExportName(SpatialMapExporter exporter, string spaceToken)
        {
            return (string)Call(exporter, "BuildExportName", spaceToken);
        }

        /// <summary>Calls a private method; a null target means a static one.</summary>
        private static object Call(SpatialMapExporter target, string name, params object[] arguments)
        {
            var flags = BindingFlags.NonPublic | (target == null ? BindingFlags.Static : BindingFlags.Instance);
            var method = typeof(SpatialMapExporter).GetMethod(name, flags);
            Assert.IsNotNull(method, $"SpatialMapExporter.{name} was renamed or moved: update this test");
            return method.Invoke(target, arguments);
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} not found");
            field.SetValue(target, value);
        }

        // ---- comparing JSON ----

        /// <summary>The extras as written to the file, with the time replaced by a placeholder.</summary>
        private static JObject WithPlaceholderTime(JObject extras)
        {
            var written = (JObject)ParseText(extras.ToString());
            written["timestamp_utc"] = TimestampPlaceholder;
            return written;
        }

        /// <summary>
        /// Reads JSON text as the consumers of the file do: a string stays a string, even when it
        /// looks like a date (JObject.Parse would turn "timestamp_utc" into a date).
        /// </summary>
        private static JToken ParseText(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
            {
                return JToken.Load(reader);
            }
        }

        /// <summary>
        /// Compares what the file would hold (the actual JSON as text and back, so that a float and
        /// a number read from text are alike). Numbers may differ by 1e-4, the order of members
        /// does not matter, and every difference is listed.
        /// </summary>
        private static void AssertSameJson(JToken expected, JToken actual, string what)
        {
            var written = ParseText(actual.ToString());
            var differences = new List<string>();
            Compare(expected, written, "$", differences);

            Assert.IsEmpty(differences, what + ": " + string.Join("; ", differences) + "\nactual:\n" + written);
        }

        private static void Compare(JToken expected, JToken actual, string path, List<string> differences)
        {
            if (expected == null || actual == null)
            {
                if (expected != actual)
                {
                    differences.Add($"{path}: expected {Show(expected)} but was {Show(actual)}");
                }

                return;
            }

            if (IsNumber(expected) && IsNumber(actual))
            {
                if (Math.Abs((double)expected - (double)actual) > Tolerance)
                {
                    differences.Add($"{path}: expected {Show(expected)} but was {Show(actual)}");
                }

                return;
            }

            if (expected.Type != actual.Type)
            {
                differences.Add($"{path}: expected {Show(expected)} but was {Show(actual)}");
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
                            differences.Add($"{path}.{property.Name}: not expected, but it is {Show(property.Value)}");
                        }
                    }

                    break;
                case JTokenType.Array:
                    var expectedArray = (JArray)expected;
                    var actualArray = (JArray)actual;
                    if (expectedArray.Count != actualArray.Count)
                    {
                        differences.Add($"{path}: expected {expectedArray.Count} items but there are {actualArray.Count}");
                    }

                    for (int i = 0; i < Math.Min(expectedArray.Count, actualArray.Count); i++)
                    {
                        Compare(expectedArray[i], actualArray[i], $"{path}[{i}]", differences);
                    }

                    break;
                default:
                    if (!JToken.DeepEquals(expected, actual))
                    {
                        differences.Add($"{path}: expected {Show(expected)} but was {Show(actual)}");
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
            return token == null ? "nothing" : token.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
