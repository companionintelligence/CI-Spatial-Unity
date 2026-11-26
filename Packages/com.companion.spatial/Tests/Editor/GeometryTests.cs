// Tests for the small value types the three modules share: Vec3, Quat, Vec2 and NormalizedBox.

using System.Collections.Generic;
using NUnit.Framework;

namespace Companion.Spatial.Tests
{
    public class GeometryTests
    {
        private const double Tolerance = 1e-12;

        // ---- Vec3 ----

        [Test]
        public void Vec3_AddSubtractAndScale_WorkComponentWise()
        {
            var a = new Vec3(1, 2, 3);
            var b = new Vec3(4, 5, 6);

            AssertVec(new Vec3(5, 7, 9), a + b);
            AssertVec(new Vec3(-3, -3, -3), a - b);
            AssertVec(new Vec3(2, 4, 6), a * 2);
            AssertVec(new Vec3(2, 4, 6), 2 * a);
            AssertVec(new Vec3(-1, -2, -3), -a);
        }

        [Test]
        public void Vec3_Dot_IsTheSumOfComponentProducts()
        {
            Assert.AreEqual(32.0, Vec3.Dot(new Vec3(1, 2, 3), new Vec3(4, 5, 6)), Tolerance);
            Assert.AreEqual(0.0, Vec3.Dot(new Vec3(1, 0, 0), new Vec3(0, 1, 0)), Tolerance);
        }

        [Test]
        public void Vec3_Distance_IsEuclideanAndSymmetric()
        {
            var a = new Vec3(0, 0, 0);
            var b = new Vec3(3, 4, 12);

            Assert.AreEqual(13.0, Vec3.Distance(a, b), Tolerance);
            Assert.AreEqual(13.0, Vec3.Distance(b, a), Tolerance);
            Assert.AreEqual(0.0, Vec3.Distance(b, b), Tolerance);
        }

        [TestCase(0.0, 10.0, 20.0, 30.0)]
        [TestCase(1.0, 20.0, 40.0, 60.0)]
        [TestCase(0.5, 15.0, 30.0, 45.0)]
        [TestCase(0.25, 12.5, 25.0, 37.5)]
        public void Vec3_Lerp_BlendsFromTheFirstTowardsTheSecond(double t, double x, double y, double z)
        {
            AssertVec(new Vec3(x, y, z), Vec3.Lerp(new Vec3(10, 20, 30), new Vec3(20, 40, 60), t));
        }

        // Like UnityEngine.Vector3.Lerp, the factor is clamped, so a smoothing alpha above 1 cannot overshoot.
        [TestCase(2.0, 20.0)]
        [TestCase(-1.0, 10.0)]
        [TestCase(double.NaN, 10.0)]
        public void Vec3_Lerp_ClampsTheFactorToZeroAndOne(double t, double expectedX)
        {
            Assert.AreEqual(expectedX, Vec3.Lerp(new Vec3(10, 0, 0), new Vec3(20, 0, 0), t).X, Tolerance);
        }

        [Test]
        public void Vec3_LengthAndNormalized_GiveAUnitVectorOrZero()
        {
            Assert.AreEqual(5.0, new Vec3(3, 4, 0).Length, Tolerance);
            AssertVec(new Vec3(0.6, 0.8, 0), new Vec3(3, 4, 0).Normalized());
            AssertVec(Vec3.Zero, Vec3.Zero.Normalized());
            AssertVec(Vec3.Zero, new Vec3(double.NaN, 1, 1).Normalized());
        }

        [Test]
        public void Vec3_IsFinite_IsFalseForNaNAndInfinity()
        {
            Assert.IsTrue(new Vec3(1, 2, 3).IsFinite);
            Assert.IsFalse(new Vec3(double.NaN, 2, 3).IsFinite);
            Assert.IsFalse(new Vec3(1, double.PositiveInfinity, 3).IsFinite);
            Assert.IsFalse(new Vec3(1, 2, double.NegativeInfinity).IsFinite);
        }

        [Test]
        public void Vec3_MinAndMax_AreComponentWise()
        {
            var a = new Vec3(1, 8, 3);
            var b = new Vec3(4, 5, 0);

            AssertVec(new Vec3(1, 5, 0), Vec3.Min(a, b));
            AssertVec(new Vec3(4, 8, 3), Vec3.Max(a, b));
        }

        [Test]
        public void Vec3_Equality_IsByValue_AndToStringIsInvariant()
        {
            Assert.IsTrue(new Vec3(1, 2, 3) == new Vec3(1, 2, 3));
            Assert.IsTrue(new Vec3(1, 2, 3) != new Vec3(1, 2, 4));
            Assert.AreEqual(new Vec3(1, 2, 3), new Vec3(1, 2, 3));
            Assert.AreEqual(new Vec3(1, 2, 3).GetHashCode(), new Vec3(1, 2, 3).GetHashCode());
            Assert.AreEqual("(1.5, -2, 0.25)", new Vec3(1.5, -2, 0.25).ToString());
        }

        // ---- Vec2 and Quat ----

        [Test]
        public void Vec2_HoldsTwoComponents_AndEqualsByValue()
        {
            var v = new Vec2(1.5, -2);

            Assert.AreEqual(1.5, v.X);
            Assert.AreEqual(-2.0, v.Y);
            Assert.AreEqual(new Vec2(1.5, -2), v);
            Assert.IsFalse(v.Equals(new Vec2(1.5, 2)));
            Assert.IsTrue(v.IsFinite);
            Assert.IsFalse(new Vec2(double.NaN, 0).IsFinite);
        }

        [Test]
        public void Quat_IdentityIsTheNoRotationQuaternion()
        {
            Assert.AreEqual(new Quat(0, 0, 0, 1), Quat.Identity);
            Assert.IsTrue(Quat.Identity.IsFinite);
            Assert.IsFalse(new Quat(0, 0, double.NaN, 1).IsFinite);
        }

        // ---- NormalizedBox ----

        [Test]
        public void NormalizedBox_TryCreate_KeepsAValidBox()
        {
            Assert.IsTrue(NormalizedBox.TryCreate(0.1, 0.2, 0.5, 0.8, out var box));

            Assert.AreEqual(0.1, box.X1, Tolerance);
            Assert.AreEqual(0.2, box.Y1, Tolerance);
            Assert.AreEqual(0.5, box.X2, Tolerance);
            Assert.AreEqual(0.8, box.Y2, Tolerance);
            Assert.AreEqual(0.4, box.Width, Tolerance);
            Assert.AreEqual(0.6, box.Height, Tolerance);
            Assert.AreEqual(0.3, box.CenterX, Tolerance);
            Assert.AreEqual(0.5, box.CenterY, Tolerance);
        }

        [Test]
        public void NormalizedBox_TryCreate_PutsReversedCornersInOrder()
        {
            Assert.IsTrue(NormalizedBox.TryCreate(0.5, 0.8, 0.1, 0.2, out var box));

            Assert.AreEqual(new NormalizedBox(0.1, 0.2, 0.5, 0.8), box);
        }

        // The scanner's pixel-or-normalized guess read x2 = 1.0001 as pixels and made a speck; here a box is always a
        // fraction of the image, so it is clamped to the image instead.
        [Test]
        public void NormalizedBox_TryCreate_ClampsToTheImage_InsteadOfReadingTheBoxAsPixels()
        {
            Assert.IsTrue(NormalizedBox.TryCreate(0.2, 0.2, 1.0001, 0.9, out var box));
            Assert.AreEqual(1.0, box.X2, Tolerance);

            Assert.IsTrue(NormalizedBox.TryCreate(-0.1, 0.2, 0.6, 1.5, out var other));
            Assert.AreEqual(new NormalizedBox(0, 0.2, 0.6, 1), other);
        }

        [Test]
        public void NormalizedBox_TryCreate_AcceptsTheWholeImage()
        {
            Assert.IsTrue(NormalizedBox.TryCreate(0, 0, 1, 1, out var box));
            Assert.AreEqual(1.0, box.Width, Tolerance);
            Assert.AreEqual(1.0, box.Height, Tolerance);
        }

        private static IEnumerable<TestCaseData> InvalidBoxes()
        {
            yield return new TestCaseData(0.2, 0.2, 0.2, 0.5).SetName("NoWidth");
            yield return new TestCaseData(0.2, 0.2, 0.5, 0.2).SetName("NoHeight");
            yield return new TestCaseData(1.5, 0.0, 2.0, 1.0).SetName("EntirelyRightOfTheImage");
            yield return new TestCaseData(-2.0, 0.0, -1.0, 1.0).SetName("EntirelyLeftOfTheImage");
            yield return new TestCaseData(double.NaN, 0.0, 0.5, 0.5).SetName("NaN");
            yield return new TestCaseData(0.0, 0.0, double.PositiveInfinity, 0.5).SetName("Infinity");
        }

        [TestCaseSource(nameof(InvalidBoxes))]
        public void NormalizedBox_TryCreate_RejectsABoxWithNoAreaInsideTheImage(double x1, double y1, double x2, double y2)
        {
            Assert.IsFalse(NormalizedBox.TryCreate(x1, y1, x2, y2, out _));
        }

        [Test]
        public void NormalizedBox_CenterSizeAndTopLeftSizeForms_AgreeWithTheCornerForm()
        {
            // The web contract's bbox_cxcywh and the scanner's bbox_normalized (x, y, width, height from the top left).
            Assert.IsTrue(NormalizedBox.TryFromCxCyWh(0.5, 0.5, 0.2, 0.4, out var fromCenter));
            Assert.AreEqual(new NormalizedBox(0.4, 0.3, 0.6, 0.7).X1, fromCenter.X1, Tolerance);
            Assert.AreEqual(0.7, fromCenter.Y2, Tolerance);

            Assert.IsTrue(NormalizedBox.TryFromXywh(0.0625, 0.125, 0.25, 0.5, out var fromCorner));
            Assert.AreEqual(0.3125, fromCorner.X2, Tolerance);
            Assert.AreEqual(0.625, fromCorner.Y2, Tolerance);

            var cxcywh = fromCorner.ToCxCyWh();
            Assert.AreEqual(4, cxcywh.Length);
            Assert.AreEqual(0.1875, cxcywh[0], Tolerance);
            Assert.AreEqual(0.375, cxcywh[1], Tolerance);
            Assert.AreEqual(0.25, cxcywh[2], Tolerance);
            Assert.AreEqual(0.5, cxcywh[3], Tolerance);

            Assert.IsTrue(NormalizedBox.TryFromCxCyWh(cxcywh[0], cxcywh[1], cxcywh[2], cxcywh[3], out var back));
            Assert.AreEqual(fromCorner.X1, back.X1, Tolerance);
            Assert.AreEqual(fromCorner.Y2, back.Y2, Tolerance);
        }

        [Test]
        public void NormalizedBox_FromCxCyWh_RejectsANegativeOrEmptySize()
        {
            Assert.IsFalse(NormalizedBox.TryFromCxCyWh(0.5, 0.5, 0.0, 0.4, out _));
            Assert.IsFalse(NormalizedBox.TryFromCxCyWh(0.5, 0.5, -0.2, 0.4, out _));
            Assert.IsFalse(NormalizedBox.TryFromXywh(0.1, 0.1, 0.2, 0.0, out _));
            Assert.IsFalse(NormalizedBox.TryFromXywh(0.1, 0.1, -0.2, 0.3, out _));
        }

        private static void AssertVec(Vec3 expected, Vec3 actual)
        {
            Assert.AreEqual(expected.X, actual.X, Tolerance, "x of " + actual);
            Assert.AreEqual(expected.Y, actual.Y, Tolerance, "y of " + actual);
            Assert.AreEqual(expected.Z, actual.Z, Tolerance, "z of " + actual);
        }
    }
}
