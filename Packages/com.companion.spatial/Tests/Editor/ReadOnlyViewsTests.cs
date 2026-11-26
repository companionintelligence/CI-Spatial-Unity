// The package keeps no static mutable state: the lists it hands out (the 16 labels, the default table, a vocabulary's
// tags, a table's tracks, a result's warnings) are read-only views, so a cast to IList cannot change what the next
// caller sees.

using System;
using System.Collections.Generic;
using Companion.Spatial.Extras;
using Companion.Spatial.Tracking;
using Companion.Spatial.Vocabulary;
using NUnit.Framework;

namespace Companion.Spatial.Tests
{
    public class ReadOnlyViewsTests
    {
        private static void AssertCannotBeChanged<T>(IReadOnlyList<T> list, T item, string what)
        {
            var asList = list as IList<T>;
            if (asList == null)
            {
                return; // not even a list that can be cast to one
            }

            Assert.IsTrue(asList.IsReadOnly, what + " is a writable list");
            Assert.Throws<NotSupportedException>(() => asList.Add(item), what);
        }

        [Test]
        public void TheSharedTables_CannotBeChangedThroughACast()
        {
            AssertCannotBeChanged(MetaLabels.All, "X", "MetaLabels.All");
            AssertCannotBeChanged(TagVocabulary.Default.Tags, "x", "TagVocabulary.Default.Tags");
            AssertCannotBeChanged(TagVocabulary.DefaultEntries, new TagEntry("x", "OTHER"), "TagVocabulary.DefaultEntries");
            AssertCannotBeChanged(TagVocabulary.DefaultEntries[0].Synonyms, "x", "a TagEntry's synonyms");
        }

        [Test]
        public void ATablesTracksAndATracksObservations_CannotBeChangedThroughACast()
        {
            var table = new TrackTable();
            table.Update(0, new[] { new Detection3D("cup", 0.5, Vec3.Zero, Vec3.Zero, "f") });

            AssertCannotBeChanged(table.Tracks, null, "TrackTable.Tracks");
            AssertCannotBeChanged(table.Tracks[0].Observations, default(TrackObservation), "Track.Observations");
        }

        [Test]
        public void TheWarningsOfAResult_CannotBeChangedThroughACast()
        {
            var read = ExtrasReader.Read("{ 'schema_version': 2, 'objects': [ 5 ] }");
            var merge = ExtrasMerge.MergeFromText("{", new ScanExtras());

            Assert.IsNotEmpty(read.Warnings);
            Assert.IsNotEmpty(merge.Warnings);
            AssertCannotBeChanged(read.Warnings, "x", "ExtrasReadResult.Warnings");
            AssertCannotBeChanged(merge.Warnings, "x", "ExtrasMergeResult.Warnings");
        }

        [Test]
        public void TwoVocabulariesAndTwoTables_ShareNothing()
        {
            var first = new TrackTable();
            var second = new TrackTable();

            first.Update(0, new[] { new Detection3D("cup", 0.5, Vec3.Zero, Vec3.Zero, "f") });

            Assert.AreEqual(1, first.Tracks.Count);
            Assert.AreEqual(0, second.Tracks.Count);
            Assert.AreEqual(1, second.Update(0, new[] { new Detection3D("cup", 0.5, Vec3.Zero, Vec3.Zero, "f") })[0].TrackId, "ids start at 1 in every table");
        }
    }
}
