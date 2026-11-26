// Tests for the running vote a track keeps for its tag and its label text (an internal helper, seen through
// InternalsVisibleTo).

using System;
using Companion.Spatial.Tracking;
using NUnit.Framework;

namespace Companion.Spatial.Tests
{
    public class ScoreVoteTests
    {
        private static ScoreVote Vote(int maxEntries = 32)
        {
            return new ScoreVote(StringComparer.OrdinalIgnoreCase, maxEntries);
        }

        [Test]
        public void ANewVote_HasNoWinner()
        {
            Assert.IsNull(Vote().Winner);
            Assert.AreEqual(0, Vote().Count);
        }

        [Test]
        public void TheEntryWithTheHighestSum_Wins()
        {
            var vote = Vote();
            vote.Add("a", "A", 0.3);
            vote.Add("b", "B", 0.5);
            Assert.AreEqual("B", vote.Winner);

            vote.Add("a", "A again", 0.3);
            Assert.AreEqual("A", vote.Winner, "0.6 beats 0.5, and the entry keeps the text of its first vote");
            Assert.AreEqual(2, vote.Count);
        }

        [Test]
        public void ATie_GoesToTheEarlierEntry()
        {
            var vote = Vote();
            vote.Add("a", "A", 0.5);
            vote.Add("b", "B", 0.5);

            Assert.AreEqual("A", vote.Winner);
        }

        [Test]
        public void KeysThatDifferOnlyInCase_AreOneEntry()
        {
            var vote = Vote();
            vote.Add("sofa", "Sofa", 0.9);
            vote.Add("SOFA", "SOFA", 0.8);
            vote.Add("couch", "couch", 1.0);

            Assert.AreEqual(2, vote.Count);
            Assert.AreEqual("Sofa", vote.Winner, "0.9 + 0.8 beats 1.0");
        }

        [Test]
        public void PastTheCap_TheWeakestOtherEntryIsDropped_NeverTheOneJustVotedFor()
        {
            var vote = Vote(3);
            vote.Add("k1", "k1", 0.9);
            vote.Add("k2", "k2", 0.8);
            vote.Add("k3", "k3", 0.7);

            vote.Add("k4", "k4", 0.01);

            Assert.AreEqual(3, vote.Count);
            Assert.AreEqual("k1", vote.Winner);

            // k4 was kept (k3 went), so another vote for it adds to its sum
            vote.Add("k4", "k4", 1.0);
            Assert.AreEqual("k4", vote.Winner);
            Assert.AreEqual(3, vote.Count);
        }

        [Test]
        public void PastTheCap_ATieForWeakestDropsTheLaterEntry()
        {
            var vote = Vote(3);
            vote.Add("k1", "k1", 0.1);
            vote.Add("k2", "k2", 0.1);
            vote.Add("k3", "k3", 0.5);
            vote.Add("k4", "k4", 0.5);

            // k2 (0.1) went; k1 (0.1, earlier) stayed. A vote for k1 adds to the old sum.
            vote.Add("k1", "k1", 0.45);
            Assert.AreEqual("k1", vote.Winner);
            Assert.AreEqual(3, vote.Count);
        }
    }
}
