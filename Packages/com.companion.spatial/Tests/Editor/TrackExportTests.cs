// The three modules together: detections are tracked, a track becomes an object of the extras, the extras are written,
// merged with an earlier export and read back.

using System;
using System.Linq;
using Companion.Spatial.Extras;
using Companion.Spatial.Tracking;
using NUnit.Framework;

namespace Companion.Spatial.Tests
{
    public class TrackExportTests
    {
        private const double Tolerance = 1e-12;

        private static Detection3D Det(string label, double score, double x, double y, double z, string frame, NormalizedBox? box = null)
        {
            return new Detection3D(label, score, new Vec3(x, y, z), new Vec3(0, 1, 0), frame, box);
        }

        private static ScanExtras Export(TrackTable table, string session, string updated)
        {
            var extras = new ScanExtras();
            extras.Space.Id = "room-1";
            extras.Space.UpdatedAtUtc = updated;
            foreach (var track in table.Tracks)
            {
                extras.Objects.Add(ScanObject.FromTrack(track, session));
            }

            return extras;
        }

        [Test]
        public void ATrack_BecomesAnObject_WithItsFusedValues()
        {
            Assert.IsTrue(NormalizedBox.TryCreate(0.1, 0.2, 0.5, 0.8, out var box));
            var table = new TrackTable();
            table.Update(0, new[] { Det("TV screen", 0.6, 1.0, 1.0, 2.0, "snap_0001", box) });
            table.Update(1, new[] { Det("monitor", 0.7, 1.1, 1.0, 2.0, "snap_0002") });

            var obj = ScanObject.FromTrack(table.Tracks.Single(), "run-a");

            Assert.AreEqual("run-a-track-1", obj.Id);
            Assert.AreEqual(1, obj.TrackId);
            Assert.AreEqual("monitor", obj.Label.Text, "0.7 for \"monitor\" beats 0.6 for \"TV screen\"");
            Assert.AreEqual("screen", obj.Label.Tag);
            Assert.AreEqual("SCREEN", obj.Label.Semantic);
            Assert.AreEqual(1 - 0.4 * 0.3, obj.Confidence, Tolerance, "noisy-OR of 0.6 and 0.7");
            Assert.AreEqual(ObjectSource.Cv, obj.Source);
            Assert.AreEqual(1.05, obj.Pose.Position.X, Tolerance, "the smoothed position");
            Assert.AreEqual(Quat.Identity, obj.Pose.Rotation);
            Assert.AreEqual(new Vec3(0, 1, 0), obj.Normal.Value);
            Assert.IsNull(obj.Bounds);
            Assert.AreEqual(2, obj.Observations.Count);
            Assert.AreEqual("snap_0001", obj.Observations[0].FrameId);
            Assert.AreEqual(box, obj.Observations[0].Box.Value);
            Assert.AreEqual(0.6, obj.Observations[0].Score, Tolerance);
            Assert.AreEqual("snap_0002", obj.Observations[1].FrameId);
            Assert.IsFalse(obj.Observations[1].Box.HasValue);
        }

        [Test]
        public void ATrackWithNoNormal_HasNoNormalInTheObject()
        {
            var table = new TrackTable();
            table.Update(0, new[] { new Detection3D("cup", 0.5, Vec3.Zero, Vec3.Zero, "f") });

            Assert.IsNull(ScanObject.FromTrack(table.Tracks[0], null).Normal);
            Assert.AreEqual("track-1", ScanObject.FromTrack(table.Tracks[0], null).Id);
        }

        [Test]
        public void FromTrack_NeedsATrack()
        {
            Assert.Throws<ArgumentNullException>(() => ScanObject.FromTrack(null, "run-a"));
        }

        [Test]
        public void TheObjectOfATrack_WritesAValidDocument_AndReadsBackTheSame()
        {
            var table = new TrackTable();
            table.Update(0, new[] { Det("sofa", 0.8, 1, 0.4, 3, "snap_0001"), Det("chair", 0.5, 2, 0.4, 1, "snap_0001") });
            table.Update(1, new[] { Det("couch", 0.6, 1.02, 0.4, 3, "snap_0002") });
            var written = ExtrasWriter.Write(Export(table, "run-a", "2026-11-25T10:00:00.0000000Z"));

            var errors = MiniSchema.Validate(written, JsonAssert.ParseObject(TestPaths.ReadSchema()));
            var back = ExtrasReader.Read(written);

            Assert.IsEmpty(errors, string.Join("; ", errors));
            Assert.IsTrue(back.Success, back.Error);
            Assert.IsEmpty(back.Warnings);
            JsonAssert.AreSame(written, ExtrasWriter.Write(back.Extras), "the objects of two tracks");
            Assert.AreEqual("couch", back.Extras.Objects[0].Label.Tag);
            Assert.AreEqual("sofa", back.Extras.Objects[0].Label.Text, "0.8 for \"sofa\" beats 0.6 for \"couch\"");
            Assert.AreEqual("COUCH", back.Extras.Objects[0].Label.Semantic);
            Assert.AreEqual("OTHER", back.Extras.Objects[1].Label.Semantic);
        }

        // The scanner's track numbers start at 1 in every app run, so two runs both have a "track 1".
        [Test]
        public void ExportingAgain_ReplacesTheRunsObjects_ButAnotherRunsObjectsAreKept()
        {
            var runA = new TrackTable();
            runA.Update(0, new[] { Det("cup", 0.6, 1, 0.8, 1, "a-snap-1") });
            var first = ExtrasMerge.Merge(null, Export(runA, "run-a", "2026-11-25T10:00:00.0000000Z")).Merged;

            runA.Update(5, new[] { Det("cup", 0.7, 1.05, 0.8, 1, "a-snap-2") });
            var again = ExtrasMerge.Merge(first, Export(runA, "run-a", "2026-11-25T10:05:00.0000000Z")).Merged;

            Assert.AreEqual(1, again.Objects.Count, "the same run exporting again replaces its object");
            Assert.AreEqual(2, again.Objects[0].Observations.Count);

            var runB = new TrackTable();
            runB.Update(0, new[] { Det("cup", 0.6, 3, 0.8, 3, "b-snap-1") });
            var both = ExtrasMerge.Merge(again, Export(runB, "run-b", "2026-11-25T11:00:00.0000000Z")).Merged;

            CollectionAssert.AreEqual(new[] { "run-a-track-1", "run-b-track-1" }, both.Objects.Select(o => o.Id).ToArray());
            Assert.AreEqual("2026-11-25T10:00:00.0000000Z", both.Space.CreatedAtUtc, "the first export's time");
            Assert.AreEqual("2026-11-25T11:00:00.0000000Z", both.Space.UpdatedAtUtc);
            Assert.AreEqual(1.025, both.Bounds.Min.X, Tolerance, "the smoothed position of run A's cup: halfway from 1.00 to 1.05");
            Assert.AreEqual(3.0, both.Bounds.Max.X, Tolerance);
        }
    }
}
