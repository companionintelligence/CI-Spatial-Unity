// Tests for the helper that picks the earliest or the latest of some ISO 8601 times held as text (an internal class,
// seen through InternalsVisibleTo).

using Companion.Spatial.Extras;
using NUnit.Framework;

namespace Companion.Spatial.Tests
{
    public class ExtrasTimeTests
    {
        [Test]
        public void Earliest_AndLatest_ComparePointsInTime()
        {
            Assert.AreEqual("2026-11-24T10:00:00Z", ExtrasTime.Earliest("2026-11-25T10:00:00Z", "2026-11-24T10:00:00Z", "2026-11-26T10:00:00Z"));
            Assert.AreEqual("2026-11-26T10:00:00Z", ExtrasTime.Latest("2026-11-25T10:00:00Z", "2026-11-24T10:00:00Z", "2026-11-26T10:00:00Z"));
        }

        [Test]
        public void ADifferentZoneOrPrecision_IsComparedByTheInstant()
        {
            // 23:30 on the 24th at -05:00 is 04:30 UTC on the 25th
            Assert.AreEqual("2026-11-25T05:00:00Z", ExtrasTime.Latest("2026-11-24T23:30:00-05:00", "2026-11-25T05:00:00Z"));
            Assert.AreEqual("2026-11-24T23:30:00-05:00", ExtrasTime.Earliest("2026-11-24T23:30:00-05:00", "2026-11-25T05:00:00Z"));
            Assert.AreEqual("2026-11-25T10:00:00.0000001Z", ExtrasTime.Latest("2026-11-25T10:00:00Z", "2026-11-25T10:00:00.0000001Z"));
        }

        [Test]
        public void ATimeWithNoZone_IsTakenAsUtc_SoTheAnswerDoesNotDependOnTheMachine()
        {
            Assert.AreEqual("2026-11-25T09:00:00", ExtrasTime.Earliest("2026-11-25T09:00:00", "2026-11-25T09:30:00Z"));
            Assert.AreEqual("2026-11-25T09:30:00Z", ExtrasTime.Latest("2026-11-25T09:00:00", "2026-11-25T09:30:00Z"));
        }

        [Test]
        public void NullEmptyAndBlankTimes_AreIgnored_AndNothingGivesNull()
        {
            Assert.AreEqual("2026-11-25T10:00:00Z", ExtrasTime.Earliest(null, "", "  ", "2026-11-25T10:00:00Z"));
            Assert.IsNull(ExtrasTime.Earliest(null, "", " "));
            Assert.IsNull(ExtrasTime.Latest());
        }

        [Test]
        public void TextThatIsNotATime_IsUsedOnlyWhenNothingElseIs()
        {
            Assert.AreEqual("2026-11-25T10:00:00Z", ExtrasTime.Earliest("<timestamp_utc>", "2026-11-25T10:00:00Z"));
            Assert.AreEqual("2026-11-25T10:00:00Z", ExtrasTime.Latest("2026-11-25T10:00:00Z", "<timestamp_utc>"));
            Assert.AreEqual("alpha", ExtrasTime.Earliest("beta", "alpha"));
            Assert.AreEqual("beta", ExtrasTime.Latest("alpha", "beta"));
        }

        [Test]
        public void TheSameInstantTwice_KeepsTheFirstText()
        {
            Assert.AreEqual("2026-11-25T10:00:00Z", ExtrasTime.Earliest("2026-11-25T10:00:00Z", "2026-11-25T10:00:00.0000000Z"));
            Assert.AreEqual("2026-11-25T10:00:00Z", ExtrasTime.Latest("2026-11-25T10:00:00Z", "2026-11-25T10:00:00.0000000Z"));
        }
    }
}
