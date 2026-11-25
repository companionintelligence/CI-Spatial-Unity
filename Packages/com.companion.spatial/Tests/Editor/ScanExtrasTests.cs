// Tests for the extras model itself: its defaults, the ids, the labels and the bounds computed from the content.

using System;
using System.Linq;
using Companion.Spatial.Extras;
using Companion.Spatial.Vocabulary;
using NUnit.Framework;

namespace Companion.Spatial.Tests
{
    public class ScanExtrasTests
    {
        private const double Tolerance = 1e-12;

        private static void AssertVec(Vec3 expected, Vec3 actual)
        {
            Assert.AreEqual(expected.X, actual.X, Tolerance, "x of " + actual);
            Assert.AreEqual(expected.Y, actual.Y, Tolerance, "y of " + actual);
            Assert.AreEqual(expected.Z, actual.Z, Tolerance, "z of " + actual);
        }

        [Test]
        public void ANewDocument_IsEmptyButComplete()
        {
            var extras = new ScanExtras();

            Assert.AreEqual("companion.scan-extras", ScanExtras.SchemaId);
            Assert.AreEqual(2, ScanExtras.CurrentVersion);
            Assert.IsNotNull(extras.Space);
            Assert.AreEqual("default", extras.Space.Id);
            Assert.AreEqual("unity-world", extras.Space.Frame);
            Assert.IsNull(extras.Space.CreatedAtUtc);
            Assert.IsNull(extras.Space.UpdatedAtUtc);
            Assert.IsNull(extras.Consent);
            Assert.IsNull(extras.Device);
            Assert.IsNull(extras.Bounds);
            Assert.IsNotNull(extras.Frames);
            Assert.IsNotNull(extras.Objects);
            Assert.IsNotNull(extras.Planes);
            Assert.IsNotNull(extras.Unknown);
            Assert.IsEmpty(extras.Frames);
            Assert.IsEmpty(extras.Objects);
            Assert.IsEmpty(extras.Planes);
            Assert.IsFalse(extras.Unknown.HasValues);
        }

        [Test]
        public void ANewObject_HasAnEmptyLabel_APoseAtTheOrigin_AndNoObservations()
        {
            var obj = new ScanObject();

            Assert.AreEqual(string.Empty, obj.Id);
            Assert.AreEqual(0, obj.TrackId);
            Assert.AreEqual(ObjectSource.Cv, obj.Source);
            Assert.AreEqual(string.Empty, obj.Label.Text);
            Assert.AreEqual(string.Empty, obj.Label.Tag);
            Assert.AreEqual("OTHER", obj.Label.Semantic);
            Assert.AreEqual(Vec3.Zero, obj.Pose.Position);
            Assert.AreEqual(Quat.Identity, obj.Pose.Rotation);
            Assert.IsNull(obj.Normal);
            Assert.IsNull(obj.Bounds);
            Assert.IsEmpty(obj.Observations);
            Assert.IsNotNull(obj.Unknown);
        }

        [Test]
        public void MakeId_JoinsTheSessionAndTheTrack_SoTrackNumbersOfDifferentRunsDoNotCollide()
        {
            Assert.AreEqual("3f9a-track-7", ScanObject.MakeId("3f9a", 7));
            Assert.AreEqual("track-7", ScanObject.MakeId(null, 7));
            Assert.AreEqual("track-7", ScanObject.MakeId(string.Empty, 7));
            Assert.AreNotEqual(ScanObject.MakeId("run-a", 1), ScanObject.MakeId("run-b", 1));
        }

        [Test]
        public void MakeAnchorId_IsTheSameForTheSameAnchor()
        {
            Assert.AreEqual("anchor-1c3f", ScanObject.MakeAnchorId("1c3f"));
            Assert.AreEqual(ScanObject.MakeAnchorId("1c3f"), ScanObject.MakeAnchorId("1c3f"));
        }

        [Test]
        public void ScanLabel_FromText_FillsTheThreeLayers()
        {
            var tv = ScanLabel.FromText("  TV screen ");
            Assert.AreEqual("TV screen", tv.Text);
            Assert.AreEqual("screen", tv.Tag);
            Assert.AreEqual("SCREEN", tv.Semantic);

            var chair = ScanLabel.FromText("chairs");
            Assert.AreEqual("chairs", chair.Text);
            Assert.AreEqual("chair", chair.Tag);
            Assert.AreEqual("OTHER", chair.Semantic);

            var nothing = ScanLabel.FromText(null);
            Assert.AreEqual(string.Empty, nothing.Text);
            Assert.AreEqual(string.Empty, nothing.Tag);
            Assert.AreEqual("OTHER", nothing.Semantic);

            var custom = ScanLabel.FromText("gizmo", new TagVocabulary(new[] { new TagEntry("widget", "TABLE", "gizmo") }));
            Assert.AreEqual("widget", custom.Tag);
            Assert.AreEqual("TABLE", custom.Semantic);
        }

        [Test]
        public void ScanBounds_FromMinMax_GivesTheCentreAndSize()
        {
            var bounds = ScanBounds.FromMinMax(new Vec3(-1, 0, 2), new Vec3(3, 2, 4));

            AssertVec(new Vec3(1, 1, 3), bounds.Center);
            AssertVec(new Vec3(4, 2, 2), bounds.Size);
            AssertVec(new Vec3(-1, 0, 2), bounds.Min);
            AssertVec(new Vec3(3, 2, 4), bounds.Max);
        }

        // ---- the bounds computed from the content ----

        [Test]
        public void ComputeBounds_OfAnEmptyDocument_IsNull()
        {
            Assert.IsNull(new ScanExtras().ComputeBounds());
        }

        [Test]
        public void ComputeBounds_OfOneObject_IsAPointAtItsPosition()
        {
            var extras = new ScanExtras();
            extras.Objects.Add(Models.CvObject("a", 1, "cup", 1, 2, 3));

            var bounds = extras.ComputeBounds();

            AssertVec(new Vec3(1, 2, 3), bounds.Center);
            AssertVec(Vec3.Zero, bounds.Size);
        }

        [Test]
        public void ComputeBounds_EnclosesEveryObjectPosition()
        {
            var extras = new ScanExtras();
            extras.Objects.Add(Models.CvObject("a", 1, "cup", 0, 0.9, 0));
            extras.Objects.Add(Models.CvObject("b", 2, "cup", 4, 0.1, 0));
            extras.Objects.Add(Models.CvObject("c", 3, "cup", 0, 0.5, 3));

            var bounds = extras.ComputeBounds();

            AssertVec(new Vec3(2, 0.5, 1.5), bounds.Center);
            AssertVec(new Vec3(4, 0.8, 3), bounds.Size);
        }

        [Test]
        public void ComputeBounds_IncludesTheExtentOfAnObjectThatHasOne()
        {
            var extras = new ScanExtras();
            var table = Models.DeviceObject("t", "TABLE", 0, 0, 0);
            table.Bounds = new ScanBounds(new Vec3(0, 0, 0), new Vec3(4, 2, 2));
            extras.Objects.Add(table);

            var bounds = extras.ComputeBounds();

            AssertVec(Vec3.Zero, bounds.Center);
            AssertVec(new Vec3(4, 2, 2), bounds.Size);
        }

        [Test]
        public void ComputeBounds_UsesAPlanesOutline_OrItsPositionWhenItHasNone()
        {
            var extras = new ScanExtras();
            extras.Planes.Add(Models.Wall("w"));
            var bare = new ScanPlane { Id = "b", Pose = Models.Pose(10, 0, 0) };
            extras.Planes.Add(bare);

            var bounds = extras.ComputeBounds();

            AssertVec(new Vec3(5, 1.25, 0), bounds.Center);
            AssertVec(new Vec3(10, 2.5, 0), bounds.Size);
        }

        [Test]
        public void ComputeBounds_IgnoresPointsThatAreNotFinite_AndNullEntries()
        {
            var extras = new ScanExtras();
            extras.Objects.Add(Models.CvObject("a", 1, "cup", 1, 1, 1));
            extras.Objects.Add(Models.CvObject("b", 2, "cup", double.NaN, 0, 0));
            extras.Objects.Add(null);
            extras.Planes.Add(null);

            var bounds = extras.ComputeBounds();

            AssertVec(new Vec3(1, 1, 1), bounds.Center);
            AssertVec(Vec3.Zero, bounds.Size);
        }

        [Test]
        public void TheFullTestModel_HasTheBoundsItsContentGives()
        {
            var full = Models.Full();
            var computed = full.ComputeBounds();

            AssertVec(full.Bounds.Center, computed.Center);
            AssertVec(full.Bounds.Size, computed.Size);
        }

        [Test]
        public void ComputeBounds_DoesNotChangeTheDocument()
        {
            var extras = Models.Full();
            var before = extras.Bounds;

            extras.ComputeBounds();

            Assert.AreSame(before, extras.Bounds);
            Assert.AreEqual(2, extras.Objects.Count);
        }
    }
}
