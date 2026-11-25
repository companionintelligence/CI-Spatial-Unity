// Table-driven tests for the track table, on synthetic detections that already carry a world position.
// Time is the scan clock in seconds; each capture is one Update call.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Companion.Spatial.Tracking;
using Companion.Spatial.Vocabulary;
using NUnit.Framework;

namespace Companion.Spatial.Tests
{
    public class TrackTableTests
    {
        private const double Tolerance = 1e-9;

        // ---- test data ----

        private static Detection3D Det(string label, double score, double x, double y, double z, string frame = "f", NormalizedBox? box = null)
        {
            return new Detection3D(label, score, new Vec3(x, y, z), new Vec3(0, 1, 0), frame, box);
        }

        private static IReadOnlyList<Detection3D> Capture(params Detection3D[] detections)
        {
            return detections;
        }

        private static string Show(IReadOnlyList<TrackEvent> events)
        {
            return string.Join(" ", events.Select(e => e.Kind + ":" + e.TrackId));
        }

        private static void AssertEvents(IReadOnlyList<TrackEvent> events, params string[] expected)
        {
            Assert.AreEqual(string.Join(" ", expected), Show(events));
        }

        private static Track TrackOf(TrackTable table, int id)
        {
            Assert.IsTrue(table.TryGetTrack(id, out var track), "track " + id + " exists");
            return track;
        }

        private static void AssertVec(Vec3 expected, Vec3 actual, string what = "")
        {
            Assert.AreEqual(expected.X, actual.X, Tolerance, what + " x");
            Assert.AreEqual(expected.Y, actual.Y, Tolerance, what + " y");
            Assert.AreEqual(expected.Z, actual.Z, Tolerance, what + " z");
        }

        // ---- the same object over five captures ----

        [Test]
        public void SameObjectOverFiveCaptures_IsOneTrack_FoundOnceThenUpdated()
        {
            var table = new TrackTable();
            var jitter = new[] { 0.00, 0.02, -0.03, 0.01, -0.01 };
            Vec3 expected = Vec3.Zero;

            for (int i = 0; i < 5; i++)
            {
                var observed = new Vec3(1.0 + jitter[i], 0.8, 2.0);
                var events = table.Update(i, Capture(new Detection3D("cup", 0.5, observed, new Vec3(0, 1, 0), "f" + i)));

                AssertEvents(events, i == 0 ? "Found:1" : "Updated:1");
                expected = i == 0 ? observed : expected + (observed - expected) * 0.5;
            }

            Assert.AreEqual(1, table.Tracks.Count);
            var track = TrackOf(table, 1);
            AssertVec(expected, track.Position, "the exponential moving average");
            AssertVec(new Vec3(0.99, 0.8, 2.0), track.LastObservedPosition, "the last raw position");
            Assert.AreEqual(1 - Math.Pow(0.5, 5), track.Confidence, Tolerance, "noisy-OR of five 0.5 scores");
            Assert.AreEqual(TrackState.Active, track.State);
            Assert.AreEqual(0.0, track.FirstSeen);
            Assert.AreEqual(4.0, track.LastSeen);
            Assert.AreEqual(5, track.ObservationCount);
            CollectionAssert.AreEqual(new[] { "f0", "f1", "f2", "f3", "f4" }, track.Observations.Select(o => o.FrameId).ToArray());
            Assert.AreEqual("cup", track.Tag);
            Assert.AreEqual("OTHER", track.SemanticLabel);
        }

        [Test]
        public void TheFirstObservationSetsThePosition_AndLaterOnesBlendWithTheSmoothingAlpha()
        {
            // alpha 0.25 is the weight of the new observation (UnityEngine Lerp(previous, new, alpha)):
            // 0 -> 0 + (0.3 - 0) * 0.25 = 0.075 -> 0.075 + (0.3 - 0.075) * 0.25 = 0.13125
            var table = new TrackTable(new TrackTableOptions(smoothingAlpha: 0.25));
            table.Update(0, Capture(Det("cup", 0.5, 0, 0, 0)));
            AssertVec(Vec3.Zero, TrackOf(table, 1).Position, "the first observation");

            table.Update(1, Capture(Det("cup", 0.5, 0.3, 0, 0)));
            Assert.AreEqual(0.075, TrackOf(table, 1).Position.X, Tolerance);

            table.Update(2, Capture(Det("cup", 0.5, 0.3, 0, 0)));
            Assert.AreEqual(0.13125, TrackOf(table, 1).Position.X, Tolerance);
        }

        [TestCase(1.0, 1.0)]
        [TestCase(0.0, 0.0)]
        public void SmoothingAlphaOfOneFollowsTheLatest_AndZeroKeepsTheFirst(double alpha, double expectedX)
        {
            var table = new TrackTable(new TrackTableOptions(smoothingAlpha: alpha));
            table.Update(0, Capture(Det("cup", 0.5, 0, 0, 0)));
            table.Update(1, Capture(Det("cup", 0.5, 0.3, 0, 0)));
            table.Update(2, Capture(Det("cup", 0.5, 1.0 * 0.3, 0, 0)));

            Assert.AreEqual(expectedX * 0.3, TrackOf(table, 1).Position.X, Tolerance);
        }

        // ---- association ----

        [Test]
        public void TwoCupsInOneCapture_AreTwoTracks_AndStayApartInLaterCaptures()
        {
            var table = new TrackTable();

            AssertEvents(table.Update(0, Capture(Det("cup", 0.6, 0.00, 0.8, 1), Det("cup", 0.6, 0.25, 0.8, 1))), "Found:1", "Found:2");

            // the cups listed the other way round: each detection goes to the cup it is nearest to
            var events = table.Update(1, Capture(Det("cup", 0.6, 0.26, 0.8, 1), Det("cup", 0.6, 0.01, 0.8, 1)));
            AssertEvents(events, "Updated:2", "Updated:1");
            Assert.AreEqual(0, events[0].DetectionIndex);
            Assert.AreEqual(1, events[1].DetectionIndex);
            Assert.AreEqual(2, table.Tracks.Count);
        }

        // Pairs are matched across the whole capture, nearest first: matching the detections in the order they are
        // listed would give the first one the second cup's track, because that is the nearest one to it.
        [Test]
        public void Association_IsNearestFirstAcrossTheCapture_NotInDetectionOrder()
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("cup", 0.6, 0.00, 0, 0), Det("cup", 0.6, 0.25, 0, 0)));

            // detection 0 is 0.20 from track 1 and 0.05 from track 2; detection 1 is 0.02 from track 2
            var events = table.Update(1, Capture(Det("cup", 0.6, 0.20, 0, 0), Det("cup", 0.6, 0.27, 0, 0)));

            AssertEvents(events, "Updated:1", "Updated:2");
        }

        [Test]
        public void TwoDetectionsInOneCapture_NeverShareATrack()
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("cup", 0.6, 0, 0, 0)));

            // both are well inside the association distance of track 1; the nearer one takes it, the other is a new cup
            var events = table.Update(1, Capture(Det("cup", 0.6, 0.10, 0, 0), Det("cup", 0.6, 0.05, 0, 0)));

            AssertEvents(events, "Found:2", "Updated:1");
            Assert.AreEqual(2, table.Tracks.Count);
        }

        [Test]
        public void ACupMovedHalfAMetre_IsANewTrack_AndTheOldOneStaysWhereItWas()
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("cup", 0.6, 0, 0.8, 1)));

            var events = table.Update(1, Capture(Det("cup", 0.6, 0.5, 0.8, 1)));

            AssertEvents(events, "Found:2");
            Assert.AreEqual(2, table.Tracks.Count);
            AssertVec(new Vec3(0, 0.8, 1), TrackOf(table, 1).Position, "the old cup");
            Assert.AreEqual(TrackState.Active, TrackOf(table, 1).State);
        }

        [TestCase(0.39, "Updated:1")]
        [TestCase(0.41, "Found:2")]
        public void TheDefaultAssociationDistanceIsFortyCentimetres(double dx, string expected)
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("cup", 0.6, 0, 0, 0)));

            AssertEvents(table.Update(1, Capture(Det("cup", 0.6, dx, 0, 0))), expected);
        }

        [TestCase(0.5, "Updated:1")]
        [TestCase(0.5000001, "Found:2")]
        public void TheAssociationDistanceIsInclusive_AndCanBeSet(double dx, string expected)
        {
            var table = new TrackTable(new TrackTableOptions(associationDistance: 0.5));
            table.Update(0, Capture(Det("cup", 0.6, 0, 0, 0)));

            AssertEvents(table.Update(1, Capture(Det("cup", 0.6, dx, 0, 0))), expected);
        }

        [Test]
        public void ADetectionIsComparedWithTheLastObservedPosition_SoASlowlyMovedObjectKeepsItsTrack()
        {
            // 0.3 m per capture: each step is within 0.4 m of the last position seen, though not of the first
            // or of the smoothed position.
            var table = new TrackTable();

            AssertEvents(table.Update(0, Capture(Det("cup", 0.6, 0.0, 0, 0))), "Found:1");
            AssertEvents(table.Update(1, Capture(Det("cup", 0.6, 0.3, 0, 0))), "Updated:1");
            AssertEvents(table.Update(2, Capture(Det("cup", 0.6, 0.6, 0, 0))), "Updated:1");
            AssertEvents(table.Update(3, Capture(Det("cup", 0.6, 0.9, 0, 0))), "Updated:1");
            Assert.AreEqual(1, table.Tracks.Count);
        }

        [Test]
        public void DetectionsOfADifferentTagNeverShareATrack()
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("cup", 0.6, 0, 0, 0)));

            AssertEvents(table.Update(1, Capture(Det("chair", 0.6, 0, 0, 0), Det("cup", 0.6, 0.01, 0, 0))), "Found:2", "Updated:1");
        }

        [Test]
        public void AnUnknownLabelIsTrackedByItsOwnTag()
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("unicorn", 0.6, 0, 0, 0)));

            AssertEvents(table.Update(1, Capture(Det("Unicorns", 0.6, 0.05, 0, 0))), "Updated:1");
            Assert.AreEqual("unicorn", TrackOf(table, 1).Tag);
            Assert.AreEqual("OTHER", TrackOf(table, 1).SemanticLabel);
        }

        // ---- the tag of a track ----

        [Test]
        public void ACouchSeenAsSofaAndLoveseat_IsOneTrackWithAStableTagAndTheMostConvincingText()
        {
            var table = new TrackTable();
            var labels = new[] { "couch", "Sofa", "sofa bed", "sofa", "a loveseat" };
            var scores = new[] { 0.4, 0.9, 0.2, 0.8, 0.1 };

            for (int i = 0; i < labels.Length; i++)
            {
                var events = table.Update(i, Capture(Det(labels[i], scores[i], 1, 0.4, 3, "f" + i)));
                AssertEvents(events, i == 0 ? "Found:1" : "Updated:1");
                Assert.AreEqual("couch", TrackOf(table, 1).Tag, "the tag after capture " + i);
            }

            var track = TrackOf(table, 1);
            Assert.AreEqual("COUCH", track.SemanticLabel);
            Assert.AreEqual("Sofa", track.Text, "\"Sofa\" and \"sofa\" add up to 1.7, the most of any spelling");
        }

        [Test]
        public void TheTextOfATrackIsTheSpellingWithTheHighestSummedScore_TiesGoToTheEarlier()
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("TV screen", 0.5, 0, 0, 0)));
            table.Update(1, Capture(Det("monitor", 0.5, 0, 0, 0)));
            Assert.AreEqual("TV screen", TrackOf(table, 1).Text, "a tie goes to the spelling seen first");

            table.Update(2, Capture(Det("monitor", 0.1, 0, 0, 0)));
            Assert.AreEqual("monitor", TrackOf(table, 1).Text);
            Assert.AreEqual("screen", TrackOf(table, 1).Tag);
        }

        // ---- confidence ----

        [Test]
        public void Confidence_IsNoisyOr_CappedAtZeroPointNineNine()
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("cup", 0.9, 0, 0, 0)));
            Assert.AreEqual(0.9, TrackOf(table, 1).Confidence, Tolerance);

            table.Update(1, Capture(Det("cup", 0.9, 0, 0, 0)));
            Assert.AreEqual(0.99, TrackOf(table, 1).Confidence, Tolerance);

            table.Update(2, Capture(Det("cup", 0.9, 0, 0, 0)));
            Assert.AreEqual(Track.MaxConfidence, TrackOf(table, 1).Confidence, "1 - 0.1^3 is 0.999, capped");
            Assert.AreEqual(0.99, Track.MaxConfidence);
        }

        [Test]
        public void ASureDetection_IsCappedToo()
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("cup", 1.0, 0, 0, 0)));

            Assert.AreEqual(0.99, TrackOf(table, 0 + 1).Confidence, Tolerance);
        }

        [Test]
        public void Confidence_OnlyEverGoesUp()
        {
            var table = new TrackTable();
            double previous = 0;
            foreach (double score in new[] { 0.3, 0.1, 0.0, 0.5, 0.2 })
            {
                table.Update(previous, Capture(Det("cup", score, 0, 0, 0)));
                double now = TrackOf(table, 1).Confidence;
                Assert.GreaterOrEqual(now, previous - Tolerance);
                previous = now;
            }
        }

        // ---- lifecycle ----

        [Test]
        public void ALostChair_IsReportedOnceAndRevivedWithTheSameId()
        {
            var table = new TrackTable();
            AssertEvents(table.Update(0, Capture(Det("chair", 0.7, 2, 0.4, 1))), "Found:1");

            AssertEvents(table.Update(5, Capture()));
            AssertEvents(table.Update(9.9, Capture()));
            Assert.AreEqual(TrackState.Active, TrackOf(table, 1).State, "not yet lost at 9.9 seconds");

            var lost = table.Update(10, Capture());
            AssertEvents(lost, "Lost:1");
            Assert.AreEqual(-1, lost[0].DetectionIndex);
            Assert.AreEqual(10.0, lost[0].Timestamp);
            Assert.AreEqual(TrackState.Lost, TrackOf(table, 1).State);

            AssertEvents(table.Update(11, Capture()));
            AssertEvents(table.Tick(15));
            Assert.AreEqual(1, table.Tracks.Count, "a lost track stays in the table");

            var revived = table.Update(20, Capture(Det("chair", 0.6, 2.05, 0.4, 1, "f20")));
            AssertEvents(revived, "Revived:1");
            Assert.AreEqual(0, revived[0].DetectionIndex);
            Assert.AreEqual(TrackState.Active, TrackOf(table, 1).State);
            Assert.AreEqual(20.0, TrackOf(table, 1).LastSeen);
            Assert.AreEqual(1, table.Tracks.Count);
        }

        [Test]
        public void Tick_ReportsALostTrackWithoutADetection_AndOnlyOnce()
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("chair", 0.7, 0, 0, 0)));

            AssertEvents(table.Tick(9.99));
            AssertEvents(table.Tick(10), "Lost:1");
            AssertEvents(table.Tick(30));
            AssertEvents(table.Update(31, Capture()));
        }

        // The scanner expired a track after 2.5 s, so a manual capture ten seconds later pinned the same chair again.
        [Test]
        public void AManualCaptureTenSecondsLater_FindsTheSameChair_NotANewOne()
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("chair", 0.7, 2, 0.4, 1)));

            var events = table.Update(10, Capture(Det("chair", 0.7, 2.02, 0.4, 1)));

            AssertEvents(events, "Lost:1", "Revived:1");
            Assert.AreEqual(1, table.Tracks.Count);
        }

        [Test]
        public void ACaptureJustUnderTheLostTime_IsAnOrdinaryUpdate()
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("chair", 0.7, 0, 0, 0)));

            AssertEvents(table.Update(9.5, Capture(Det("chair", 0.7, 0, 0, 0))), "Updated:1");
        }

        [Test]
        public void ALostTrackIsRevivedByTheNearestDetection_NotTheNearestTrackInTheTable()
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("cup", 0.6, 0, 0, 0), Det("cup", 0.6, 1, 0, 0)));

            var events = table.Update(20, Capture(Det("cup", 0.6, 1.05, 0, 0)));

            AssertEvents(events, "Lost:1", "Lost:2", "Revived:2");
            Assert.AreEqual(TrackState.Lost, TrackOf(table, 1).State);
            Assert.AreEqual(TrackState.Active, TrackOf(table, 2).State);
        }

        [Test]
        public void TheLostTimeCanBeSet()
        {
            var table = new TrackTable(new TrackTableOptions(lostAfterSeconds: 2.5));
            table.Update(0, Capture(Det("chair", 0.7, 0, 0, 0)));

            AssertEvents(table.Tick(2.4));
            AssertEvents(table.Tick(2.5), "Lost:1");
        }

        [Test]
        public void EventsOfOneUpdate_ListTheTimeBasedOnesFirst_ThenTheDetectionsInTheirOrder()
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("chair", 0.7, 0, 0, 0)));

            var events = table.Update(20, Capture(Det("cup", 0.6, 5, 0, 0), Det("chair", 0.6, 0.1, 0, 0), Det("lamp", 0.6, 7, 0, 0)));

            AssertEvents(events, "Lost:1", "Found:2", "Revived:1", "Found:3");
            CollectionAssert.AreEqual(new[] { -1, 0, 1, 2 }, events.Select(e => e.DetectionIndex).ToArray());
        }

        // ---- retiring ----

        [Test]
        public void ALostTrackStaysForeverByDefault()
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("chair", 0.7, 0, 0, 0)));

            AssertEvents(table.Update(1e9, Capture()), "Lost:1");
            Assert.AreEqual(1, table.Tracks.Count);
        }

        [Test]
        public void RetireAfterSeconds_RemovesATrackForGood_AndItsIdIsNeverUsedAgain()
        {
            var table = new TrackTable(new TrackTableOptions(retireAfterSeconds: 30));
            AssertEvents(table.Update(0, Capture(Det("chair", 0.7, 0, 0, 0))), "Found:1");
            AssertEvents(table.Update(10, Capture()), "Lost:1");
            AssertEvents(table.Update(29.9, Capture()));
            Assert.AreEqual(1, table.Tracks.Count);

            AssertEvents(table.Update(30, Capture()), "Retired:1");
            Assert.AreEqual(0, table.Tracks.Count);
            Assert.IsFalse(table.TryGetTrack(1, out _));

            AssertEvents(table.Update(31, Capture(Det("chair", 0.7, 0, 0, 0))), "Found:2");
        }

        [Test]
        public void ATrackThatIsBothLostAndRetiredInOneStep_IsReportedLostThenRetired()
        {
            var table = new TrackTable(new TrackTableOptions(retireAfterSeconds: 30));
            table.Update(0, Capture(Det("chair", 0.7, 0, 0, 0)));

            AssertEvents(table.Tick(100), "Lost:1", "Retired:1");
        }

        [Test]
        public void SeveralTracksLostAndRetiredInOneStep_AreReportedTrackByTrack_InIdOrder()
        {
            var table = new TrackTable(new TrackTableOptions(retireAfterSeconds: 30));
            table.Update(0, Capture(Det("cup", 0.7, 0, 0, 0), Det("chair", 0.7, 5, 0, 0)));

            AssertEvents(table.Tick(100), "Lost:1", "Retired:1", "Lost:2", "Retired:2");
            Assert.AreEqual(0, table.Tracks.Count);
        }

        [Test]
        public void ADetectionAfterTheRetireTime_IsANewTrack_NotARevival()
        {
            var table = new TrackTable(new TrackTableOptions(retireAfterSeconds: 30));
            table.Update(0, Capture(Det("chair", 0.7, 0, 0, 0)));

            AssertEvents(table.Update(40, Capture(Det("chair", 0.7, 0, 0, 0))), "Lost:1", "Retired:1", "Found:2");
        }

        // ---- ids ----

        [Test]
        public void Ids_StartAtOne_AndGoUpInTheOrderTracksAreFound()
        {
            var table = new TrackTable();

            AssertEvents(table.Update(0, Capture(Det("cup", 0.6, 0, 0, 0), Det("chair", 0.6, 1, 0, 0), Det("lamp", 0.6, 2, 0, 0))), "Found:1", "Found:2", "Found:3");
            AssertEvents(table.Update(1, Capture(Det("book", 0.6, 3, 0, 0))), "Found:4");
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, table.Tracks.Select(t => t.Id).ToArray());
        }

        // ---- the history of observations ----

        [Test]
        public void TheObservationHistory_KeepsTheSixteenMostRecent_AndCountsAll()
        {
            var table = new TrackTable();
            for (int i = 0; i < 20; i++)
            {
                table.Update(i, Capture(Det("cup", 0.5, 0, 0, 0, "f" + i)));
            }

            var track = TrackOf(table, 1);
            Assert.AreEqual(20, track.ObservationCount);
            Assert.AreEqual(16, track.Observations.Count);
            Assert.AreEqual("f4", track.Observations[0].FrameId);
            Assert.AreEqual("f19", track.Observations[15].FrameId);
        }

        [Test]
        public void TheObservationHistoryLengthCanBeSet()
        {
            var table = new TrackTable(new TrackTableOptions(maxObservations: 3));
            for (int i = 0; i < 5; i++)
            {
                table.Update(i, Capture(Det("cup", 0.5, 0, 0, 0, "f" + i)));
            }

            CollectionAssert.AreEqual(new[] { "f2", "f3", "f4" }, TrackOf(table, 1).Observations.Select(o => o.FrameId).ToArray());
        }

        [Test]
        public void AnObservation_HoldsTheFrameTheBoxAndTheScore()
        {
            Assert.IsTrue(NormalizedBox.TryCreate(0.1, 0.2, 0.5, 0.8, out var box));
            var table = new TrackTable();
            table.Update(0, Capture(Det("cup", 0.75, 0, 0, 0, "snap_0001", box), Det("cup", 0.5, 3, 0, 0, "snap_0001")));

            var first = TrackOf(table, 1).Observations.Single();
            Assert.AreEqual("snap_0001", first.FrameId);
            Assert.AreEqual(0.75, first.Score);
            Assert.IsTrue(first.Box.HasValue);
            Assert.AreEqual(box, first.Box.Value);
            Assert.IsFalse(TrackOf(table, 2).Observations.Single().Box.HasValue, "a detection without a box has none");
        }

        [Test]
        public void ANullFrameId_BecomesAnEmptyOne()
        {
            var table = new TrackTable();
            table.Update(0, Capture(new Detection3D("cup", 0.5, Vec3.Zero, Vec3.Zero, null)));

            Assert.AreEqual(string.Empty, TrackOf(table, 1).Observations.Single().FrameId);
        }

        // ---- normals ----

        [Test]
        public void TheNormal_IsTheSmoothedDirection_AndAZeroNormalIsIgnored()
        {
            var table = new TrackTable();
            table.Update(0, Capture(new Detection3D("cup", 0.5, Vec3.Zero, new Vec3(0, 1, 0), "f")));
            table.Update(1, Capture(new Detection3D("cup", 0.5, Vec3.Zero, new Vec3(1, 0, 0), "f")));

            var blended = TrackOf(table, 1).Normal;
            Assert.AreEqual(1.0, blended.Length, Tolerance);
            Assert.AreEqual(Math.Sqrt(0.5), blended.X, Tolerance);
            Assert.AreEqual(Math.Sqrt(0.5), blended.Y, Tolerance);

            table.Update(2, Capture(new Detection3D("cup", 0.5, Vec3.Zero, Vec3.Zero, "f")));
            AssertVec(blended, TrackOf(table, 1).Normal, "a zero normal leaves it alone");
        }

        [Test]
        public void NormalsThatPointOppositeWays_AndCancelOut_LeaveTheNewestOne()
        {
            var table = new TrackTable();
            table.Update(0, Capture(new Detection3D("cup", 0.5, Vec3.Zero, new Vec3(0, 1, 0), "f")));
            table.Update(1, Capture(new Detection3D("cup", 0.5, Vec3.Zero, new Vec3(0, -1, 0), "f")));

            AssertVec(new Vec3(0, -1, 0), TrackOf(table, 1).Normal, "an average of zero has no direction, so the newest stands");
        }

        [Test]
        public void ATrackWhoseDetectionsHadNoNormal_HasAZeroNormalUntilOneArrives()
        {
            var table = new TrackTable();
            table.Update(0, Capture(new Detection3D("cup", 0.5, Vec3.Zero, Vec3.Zero, "f")));
            AssertVec(Vec3.Zero, TrackOf(table, 1).Normal);

            table.Update(1, Capture(new Detection3D("cup", 0.5, Vec3.Zero, new Vec3(0, 0, 2), "f")));
            AssertVec(new Vec3(0, 0, 1), TrackOf(table, 1).Normal, "a normal is stored as a unit vector");
        }

        // ---- empty, invalid and odd input ----

        [Test]
        public void AnEmptyCapture_ChangesNothing()
        {
            var table = new TrackTable();

            AssertEvents(table.Update(1, Capture()));
            AssertEvents(table.Update(2, null));
            AssertEvents(table.Tick(3));
            Assert.AreEqual(0, table.Tracks.Count);
            Assert.AreEqual(0, table.RejectedDetections);
        }

        private static IEnumerable<TestCaseData> RejectedDetections()
        {
            yield return new TestCaseData(Det("cup", double.NaN, 0, 0, 0)).SetName("Rejected_NaNScore");
            yield return new TestCaseData(Det("cup", 0.5, double.NaN, 0, 0)).SetName("Rejected_NaNPosition");
            yield return new TestCaseData(Det("cup", 0.5, 0, double.PositiveInfinity, 0)).SetName("Rejected_InfinitePosition");
            yield return new TestCaseData(Det("", 0.5, 0, 0, 0)).SetName("Rejected_EmptyLabel");
            yield return new TestCaseData(Det(null, 0.5, 0, 0, 0)).SetName("Rejected_NullLabel");
            yield return new TestCaseData(Det("   ", 0.5, 0, 0, 0)).SetName("Rejected_BlankLabel");
            yield return new TestCaseData(Det("123", 0.5, 0, 0, 0)).SetName("Rejected_GarbageLabel");
            yield return new TestCaseData(default(Detection3D)).SetName("Rejected_DefaultDetection");
        }

        [TestCaseSource(nameof(RejectedDetections))]
        public void ADetectionThatCannotBeTracked_IsIgnoredAndCounted(Detection3D bad)
        {
            var table = new TrackTable();

            var events = table.Update(1, Capture(bad, Det("cup", 0.5, 5, 0, 0)));

            AssertEvents(events, "Found:1");
            Assert.AreEqual(1, events[0].DetectionIndex, "the good detection keeps its index in the capture");
            Assert.AreEqual(1, table.Tracks.Count);
            Assert.AreEqual(1, table.RejectedDetections);
        }

        [Test]
        public void ANegativeScore_IsClampedToZero_AndABigOneToOne()
        {
            var table = new TrackTable();
            AssertEvents(table.Update(0, Capture(Det("cup", -0.5, 0, 0, 0), Det("lamp", 7.0, 3, 0, 0))), "Found:1", "Found:2");

            Assert.AreEqual(0.0, TrackOf(table, 1).Confidence, Tolerance);
            Assert.AreEqual(0.0, TrackOf(table, 1).Observations.Single().Score, Tolerance);
            Assert.AreEqual(0.99, TrackOf(table, 2).Confidence, Tolerance);
            Assert.AreEqual(1.0, TrackOf(table, 2).Observations.Single().Score, Tolerance);
            Assert.AreEqual(0, table.RejectedDetections);
        }

        [Test]
        public void AnInfiniteScore_IsClamped_NotRejected()
        {
            var table = new TrackTable();

            AssertEvents(table.Update(0, Capture(Det("cup", double.PositiveInfinity, 0, 0, 0), Det("lamp", double.NegativeInfinity, 3, 0, 0))), "Found:1", "Found:2");
            Assert.AreEqual(0.99, TrackOf(table, 1).Confidence, Tolerance);
            Assert.AreEqual(0.0, TrackOf(table, 2).Confidence, Tolerance);
        }

        [Test]
        public void ARejectedDetection_DoesNotTouchAnExistingTrack()
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("cup", 0.5, 0, 0, 0)));

            AssertEvents(table.Update(1, Capture(Det("cup", double.NaN, 0.01, 0, 0))));
            Assert.AreEqual(1, TrackOf(table, 1).ObservationCount);
            Assert.AreEqual(0.0, TrackOf(table, 1).LastSeen);
        }

        [Test]
        public void TimeNeverGoesBackwards_ANonFiniteOrEarlierTimestampIsTakenAsTheLatestOne()
        {
            var table = new TrackTable();
            table.Update(10, Capture(Det("cup", 0.5, 0, 0, 0)));

            table.Update(5, Capture(Det("cup", 0.5, 0, 0, 0)));
            Assert.AreEqual(10.0, table.Clock);
            Assert.AreEqual(10.0, TrackOf(table, 1).LastSeen);

            table.Update(double.NaN, Capture(Det("cup", 0.5, 0, 0, 0)));
            table.Update(double.NegativeInfinity, Capture());
            Assert.AreEqual(10.0, table.Clock);

            table.Update(12, Capture());
            Assert.AreEqual(12.0, table.Clock);
        }

        [Test]
        public void ANegativeFirstTimestamp_IsFine()
        {
            var table = new TrackTable();

            AssertEvents(table.Update(-5, Capture(Det("cup", 0.5, 0, 0, 0))), "Found:1");
            Assert.AreEqual(-5.0, TrackOf(table, 1).FirstSeen);
        }

        // ---- events ----

        [Test]
        public void Events_NameTheTrackTheDetectionAndTheTime()
        {
            var table = new TrackTable();

            var events = table.Update(3.5, Capture(Det("cup", 0.6, 0, 0, 0), Det("chair", 0.6, 9, 0, 0)));

            Assert.AreEqual(TrackEventKind.Found, events[0].Kind);
            Assert.AreEqual(1, events[0].TrackId);
            Assert.AreEqual(0, events[0].DetectionIndex);
            Assert.AreEqual(3.5, events[0].Timestamp);
            Assert.AreEqual(2, events[1].TrackId);
            Assert.AreEqual(1, events[1].DetectionIndex);
        }

        [Test]
        public void TheEventListOfAnUpdate_IsACopyTheCallerMayKeep()
        {
            var table = new TrackTable();
            var first = table.Update(0, Capture(Det("cup", 0.6, 0, 0, 0)));
            table.Update(1, Capture(Det("cup", 0.6, 0, 0, 0)));

            AssertEvents(first, "Found:1");
        }

        // ---- options ----

        [Test]
        public void TheDefaultOptions_AreThoseOfTheScannersPipeline_ExceptTheLostTime()
        {
            var options = new TrackTableOptions();

            Assert.AreEqual(0.4, options.AssociationDistance, "trackDistanceThreshold");
            Assert.AreEqual(0.5, options.SmoothingAlpha, "smoothingAlpha");
            Assert.AreEqual(10.0, options.LostAfterSeconds, "the pipeline expired a track after 2.5 s (trackMaxAgeSeconds)");
            Assert.IsTrue(double.IsPositiveInfinity(options.RetireAfterSeconds), "never");
            Assert.AreEqual(16, options.MaxObservations);
            Assert.AreSame(TagVocabulary.Default, options.Vocabulary);
        }

        [Test]
        public void ABrokenOption_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackTableOptions(associationDistance: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackTableOptions(associationDistance: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackTableOptions(associationDistance: double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackTableOptions(smoothingAlpha: -0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackTableOptions(smoothingAlpha: 1.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackTableOptions(smoothingAlpha: double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackTableOptions(lostAfterSeconds: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackTableOptions(lostAfterSeconds: double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackTableOptions(maxObservations: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackTableOptions(lostAfterSeconds: 10, retireAfterSeconds: 5), "a track cannot be retired before it is lost");
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackTableOptions(retireAfterSeconds: double.NaN));
            Assert.Throws<ArgumentNullException>(() => new TrackTable(null));
            Assert.DoesNotThrow(() => new TrackTableOptions(lostAfterSeconds: 10, retireAfterSeconds: 10));
        }

        // ---- determinism and scale ----

        // A fixed linear congruential sequence: the same in every runtime, with no System.Random.
        private sealed class Lcg
        {
            private ulong _state;

            public Lcg(ulong seed)
            {
                _state = seed;
            }

            public double Next()
            {
                _state = _state * 6364136223846793005UL + 1442695040888963407UL;
                return (_state >> 11) / (double)(1UL << 53);
            }
        }

        private static string RunScenario(TrackTable table)
        {
            var random = new Lcg(12345);
            var labels = new[] { "cup", "chair", "sofa", "couch", "monitor", "lamp" };
            var log = new StringBuilder();
            for (int capture = 0; capture < 120; capture++)
            {
                var detections = new List<Detection3D>();
                int count = (int)(random.Next() * 7);
                for (int i = 0; i < count; i++)
                {
                    string label = labels[(int)(random.Next() * labels.Length)];
                    double score = random.Next() * 1.2 - 0.1;
                    var position = new Vec3(Math.Round(random.Next() * 3, 2), Math.Round(random.Next(), 2), Math.Round(random.Next() * 3, 2));
                    detections.Add(new Detection3D(label, score, position, new Vec3(0, 1, 0), "f" + capture));
                }

                var events = table.Update(capture * 1.7, detections);
                log.Append(capture).Append(':').Append(Show(events)).Append('\n');
            }

            foreach (var track in table.Tracks)
            {
                log.Append(Dump(track)).Append('\n');
            }

            return log.ToString();
        }

        private static string Dump(Track t)
        {
            var c = CultureInfo.InvariantCulture;
            return string.Format(c, "#{0} {1}|{2}|{3} {4} p=({5:R},{6:R},{7:R}) n=({8:R},{9:R},{10:R}) c={11:R} first={12:R} last={13:R} obs={14}/{15}",
                t.Id, t.Tag, t.Text, t.State, t.SemanticLabel, t.Position.X, t.Position.Y, t.Position.Z, t.Normal.X, t.Normal.Y, t.Normal.Z,
                t.Confidence, t.FirstSeen, t.LastSeen, t.Observations.Count, t.ObservationCount);
        }

        [Test]
        public void TheSameCapturesTwice_GiveIdenticalEventsAndTracks()
        {
            string first = RunScenario(new TrackTable());
            string second = RunScenario(new TrackTable());

            Assert.AreEqual(first, second);
            Assert.IsTrue(first.Length > 1000, "the scenario made tracks and events");
        }

        [Test]
        public void TheSameCapturesTwice_GiveIdenticalResultsWithRetiringOn()
        {
            var options = new TrackTableOptions(lostAfterSeconds: 4, retireAfterSeconds: 12);

            Assert.AreEqual(RunScenario(new TrackTable(options)), RunScenario(new TrackTable(options)));
        }

        [Test]
        public void TenObjectsSeenTwoHundredTimes_StayTenTracks()
        {
            var table = new TrackTable();
            var random = new Lcg(7);
            for (int capture = 0; capture < 200; capture++)
            {
                var detections = new List<Detection3D>();
                for (int i = 0; i < 10; i++)
                {
                    // ten cups one metre apart, each seen with up to 5 cm of noise
                    detections.Add(Det("cup", 0.5, i + (random.Next() - 0.5) * 0.1, 0.8 + (random.Next() - 0.5) * 0.1, 2 + (random.Next() - 0.5) * 0.1));
                }

                table.Update(capture, detections);
            }

            Assert.AreEqual(10, table.Tracks.Count);
            Assert.IsTrue(table.Tracks.All(t => t.ObservationCount == 200));
            Assert.IsTrue(table.Tracks.All(t => t.State == TrackState.Active));
        }

        // A track keeps a running vote of the spellings it has seen; a stream of one-off spellings must not grow it
        // without limit, and the best-scored spelling must survive the churn.
        [Test]
        public void ManyOneOffSpellings_DoNotUnseatTheBestOne()
        {
            var spellings = Enumerable.Range(0, 60).Select(i => "spelling " + (char)('a' + i / 26) + (char)('a' + i % 26)).ToArray();
            var vocabulary = new TagVocabulary(new[] { new TagEntry("widget", "OTHER", spellings.Concat(new[] { "keeper" }).ToArray()) });
            var table = new TrackTable(new TrackTableOptions(vocabulary: vocabulary));

            table.Update(0, Capture(Det("keeper", 0.9, 0, 0, 0)));
            for (int i = 0; i < spellings.Length; i++)
            {
                table.Update(i + 1, Capture(Det(spellings[i], 0.1, 0, 0, 0)));
            }

            Assert.AreEqual(1, table.Tracks.Count);
            Assert.AreEqual("keeper", TrackOf(table, 1).Text);
            Assert.AreEqual("widget", TrackOf(table, 1).Tag);
            Assert.AreEqual(61, TrackOf(table, 1).ObservationCount);
        }

        [Test]
        public void ATrackKnowsItsObjectsMetaLabel()
        {
            var table = new TrackTable();
            table.Update(0, Capture(Det("monitor", 0.6, 0, 0, 0), Det("chair", 0.6, 3, 0, 0), Det("a couch", 0.6, 6, 0, 0)));

            CollectionAssert.AreEqual(new[] { "SCREEN", "OTHER", "COUCH" }, table.Tracks.Select(t => t.SemanticLabel).ToArray());
            CollectionAssert.AreEqual(new[] { "screen", "chair", "couch" }, table.Tracks.Select(t => t.Tag).ToArray());
        }

        [Test]
        public void ACustomVocabulary_DecidesWhichDetectionsAreTheSameObject()
        {
            var vocabulary = new TagVocabulary(new[]
            {
                new TagEntry("tv", "SCREEN"),
                new TagEntry("monitor", "SCREEN")
            });
            var table = new TrackTable(new TrackTableOptions(vocabulary: vocabulary));
            table.Update(0, Capture(Det("tv", 0.6, 0, 0, 0)));

            AssertEvents(table.Update(1, Capture(Det("monitor", 0.6, 0.01, 0, 0))), "Found:2");
        }
    }
}
