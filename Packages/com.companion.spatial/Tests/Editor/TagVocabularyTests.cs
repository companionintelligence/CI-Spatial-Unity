// Table-driven tests for the tag vocabulary: raw detector text -> canonical tag -> one of Meta's 16 semantic labels.
// The expected values for the old doubtful cases come from the scanner's characterization tests
// (SemanticLabelMapperCharacterizationTests, the tests named "Currently...").

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Companion.Spatial.Vocabulary;
using NUnit.Framework;

namespace Companion.Spatial.Tests
{
    public class TagVocabularyTests
    {
        // SpatialCapturePipeline.DefaultGroundingDinoPrompt, copied: the 26 classes the scanner asks GroundingDINO for.
        internal const string DefaultGroundingDinoPrompt =
            "chair . table . sofa . bed . lamp . cup . mug . bottle . bowl . book . laptop . monitor . " +
            "keyboard . phone . remote control . plant . vase . clock . picture frame . shelf . cabinet . " +
            "door . window . box . bag . pillow .";

        private static readonly TagVocabulary Vocabulary = TagVocabulary.Default;

        // ---- the 26 classes of the default prompt ----

        // class, canonical tag, Meta label. 11 of the 26 get a Meta label (the scanner gave 10: picture frame was OTHER).
        private static readonly string[][] DefaultPromptTable =
        {
            new[] { "chair", "chair", "OTHER" },
            new[] { "table", "table", "TABLE" },
            new[] { "sofa", "couch", "COUCH" },
            new[] { "bed", "bed", "BED" },
            new[] { "lamp", "lamp", "LAMP" },
            new[] { "cup", "cup", "OTHER" },
            new[] { "mug", "mug", "OTHER" },
            new[] { "bottle", "bottle", "OTHER" },
            new[] { "bowl", "bowl", "OTHER" },
            new[] { "book", "book", "OTHER" },
            new[] { "laptop", "laptop", "OTHER" },
            new[] { "monitor", "screen", "SCREEN" },
            new[] { "keyboard", "keyboard", "OTHER" },
            new[] { "phone", "phone", "OTHER" },
            new[] { "remote control", "remote control", "OTHER" },
            new[] { "plant", "plant", "PLANT" },
            new[] { "vase", "vase", "OTHER" },
            new[] { "clock", "clock", "OTHER" },
            new[] { "picture frame", "wall art", "WALL_ART" },
            new[] { "shelf", "shelf", "STORAGE" },
            new[] { "cabinet", "cabinet", "STORAGE" },
            new[] { "door", "door", "DOOR_FRAME" },
            new[] { "window", "window", "WINDOW_FRAME" },
            new[] { "box", "box", "OTHER" },
            new[] { "bag", "bag", "OTHER" },
            new[] { "pillow", "pillow", "OTHER" }
        };

        private static IEnumerable<TestCaseData> DefaultPromptCases()
        {
            foreach (var row in DefaultPromptTable)
            {
                yield return new TestCaseData(row[0], row[1], row[2]).SetName("DefaultPrompt_" + row[0].Replace(' ', '_'));
            }
        }

        [TestCaseSource(nameof(DefaultPromptCases))]
        public void DefaultPromptClass_NormalizesAndMapsAsDecided(string raw, string tag, string semantic)
        {
            Assert.AreEqual(tag, Vocabulary.Normalize(raw));
            Assert.AreEqual(semantic, Vocabulary.ToSemanticLabel(raw));
            Assert.IsTrue(Vocabulary.IsKnownTag(tag), "\"" + tag + "\" is a valid canonical tag, so tracking can use it");
        }

        [Test]
        public void DefaultPrompt_HasTwentySixClasses_AndTheTableCoversEachOne()
        {
            var classes = DefaultGroundingDinoPrompt.Split('.').Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();

            Assert.AreEqual(26, classes.Length);
            CollectionAssert.AreEqual(classes, DefaultPromptTable.Select(row => row[0]).ToArray());
            Assert.AreEqual(11, DefaultPromptTable.Count(row => row[2] != "OTHER"));
            Assert.AreEqual(15, DefaultPromptTable.Count(row => row[2] == "OTHER"));
        }

        // ---- what the scanner got wrong ----

        private static IEnumerable<TestCaseData> FormerlyWrongCases()
        {
            // raw, tag, Meta label
            var rows = new[]
            {
                new[] { "chair", "chair", "OTHER" },
                new[] { "Chair", "chair", "OTHER" },
                new[] { " chair ", "chair", "OTHER" },
                new[] { "armchair", "chair", "OTHER" },
                new[] { "office chair", "chair", "OTHER" },
                new[] { "picture frame", "wall art", "WALL_ART" },
                new[] { "a couch", "couch", "COUCH" },
                new[] { "the table", "table", "TABLE" },
                new[] { "an armchair", "chair", "OTHER" },
                new[] { "A Lamp", "lamp", "LAMP" },
                new[] { "tables", "table", "TABLE" },
                new[] { "sofas", "couch", "COUCH" },
                new[] { "windows", "window", "WINDOW_FRAME" },
                new[] { "doors", "door", "DOOR_FRAME" },
                new[] { "plants", "plant", "PLANT" },
                new[] { "shelves", "shelf", "STORAGE" },
                new[] { "lamps", "lamp", "LAMP" },
                new[] { "TV screen", "screen", "SCREEN" },
                new[] { "Sofa Bed", "couch", "COUCH" },
                new[] { "coffee table", "table", "TABLE" },
                new[] { "floor lamp", "lamp", "LAMP" },
                new[] { "bookshelf", "shelf", "STORAGE" },
                new[] { "desk lamp", "lamp", "LAMP" },
                new[] { "door  frame", "door", "DOOR_FRAME" },
                new[] { "door - frame", "door", "DOOR_FRAME" },
                new[] { "door_ frame", "door", "DOOR_FRAME" },
                new[] { "door\tframe", "door", "DOOR_FRAME" },
                new[] { "wall--art", "wall art", "WALL_ART" },
                new[] { "couch.", "couch", "COUCH" },
                new[] { "couch ,", "couch", "COUCH" },
                new[] { "couch .", "couch", "COUCH" },
                new[] { "(couch)", "couch", "COUCH" },
                new[] { "-couch", "couch", "COUCH" },
                new[] { "couch-", "couch", "COUCH" },
                new[] { "_couch_", "couch", "COUCH" }
            };

            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                yield return new TestCaseData(row[0], row[1], row[2]).SetName("Formerly_" + (i + 1).ToString("00") + "_" + row[0].Trim().Replace(' ', '_').Replace('\t', '_'));
            }
        }

        [TestCaseSource(nameof(FormerlyWrongCases))]
        public void FormerlyWrongLabel_NowNormalizesAndMaps(string raw, string tag, string semantic)
        {
            Assert.AreEqual(tag, Vocabulary.Normalize(raw));
            Assert.AreEqual(semantic, Vocabulary.ToSemanticLabel(raw));
        }

        // A compound that is not in the table is its own (unknown) tag: the vocabulary does not guess from the last word.
        [TestCase("wall art frame", "wall art frame")]
        [TestCase("couch/sofa", "couch sofa")]
        [TestCase("unicorn", "unicorn")]
        [TestCase("couchh", "couchh")]
        [TestCase("ouch", "ouch")]
        [TestCase("x", "x")]
        public void UnknownText_IsItsOwnTag_AndOther(string raw, string tag)
        {
            Assert.AreEqual(tag, Vocabulary.Normalize(raw));
            Assert.AreEqual("OTHER", Vocabulary.ToSemanticLabel(raw));
            Assert.IsFalse(Vocabulary.IsKnownTag(tag));
        }

        // ---- Meta's 16 labels ----

        private static readonly string[][] MetaLabelTable =
        {
            new[] { "CEILING", "ceiling" },
            new[] { "DOOR_FRAME", "door" },
            new[] { "FLOOR", "floor" },
            new[] { "INVISIBLE_WALL_FACE", "invisible wall" },
            new[] { "WALL_ART", "wall art" },
            new[] { "WALL_FACE", "wall" },
            new[] { "WINDOW_FRAME", "window" },
            new[] { "COUCH", "couch" },
            new[] { "TABLE", "table" },
            new[] { "BED", "bed" },
            new[] { "LAMP", "lamp" },
            new[] { "PLANT", "plant" },
            new[] { "SCREEN", "screen" },
            new[] { "STORAGE", "storage" },
            new[] { "GLOBAL_MESH", "global mesh" },
            new[] { "OTHER", "other" }
        };

        private static IEnumerable<TestCaseData> MetaLabelCases()
        {
            foreach (var row in MetaLabelTable)
            {
                yield return new TestCaseData(row[0], row[1]).SetName("MetaLabel_" + row[0]);
            }
        }

        [TestCaseSource(nameof(MetaLabelCases))]
        public void MetaLabel_RoundTrips_ThroughItsTagAndBack(string label, string tag)
        {
            Assert.AreEqual(label, Vocabulary.ToSemanticLabel(label), "the label itself");
            Assert.AreEqual(tag, Vocabulary.Normalize(label), "its canonical tag");
            Assert.IsTrue(Vocabulary.IsKnownTag(tag));
            Assert.AreEqual(label, Vocabulary.ToSemanticLabel(tag), "the tag gives the label back");
            Assert.AreEqual(label, Vocabulary.ToSemanticLabel(label.ToLowerInvariant()), "lower case");
            Assert.AreEqual(label, Vocabulary.ToSemanticLabel(label.Replace('_', ' ')), "with spaces");
            Assert.AreEqual(label, Vocabulary.ToSemanticLabel(label.Replace('_', '-')), "with hyphens");
        }

        // INVISIBLE_WALL_FACE and GLOBAL_MESH have no detector class, but the scanner's own labels now map to them.
        [Test]
        public void InvisibleWallFaceAndGlobalMesh_CanBeProduced()
        {
            Assert.AreEqual("INVISIBLE_WALL_FACE", Vocabulary.ToSemanticLabel("invisible wall face"));
            Assert.AreEqual("INVISIBLE_WALL_FACE", Vocabulary.ToSemanticLabel("Invisible-Wall"));
            Assert.AreEqual("GLOBAL_MESH", Vocabulary.ToSemanticLabel("global mesh"));
            Assert.AreEqual("GLOBAL_MESH", Vocabulary.ToSemanticLabel("GLOBAL_MESH"));
        }

        [Test]
        public void MetaLabels_AreTheSixteenLabelsOfTheSpaceSetupExportInTheScannersOrder()
        {
            CollectionAssert.AreEqual(new[]
            {
                "CEILING", "DOOR_FRAME", "FLOOR", "INVISIBLE_WALL_FACE", "WALL_ART", "WALL_FACE", "WINDOW_FRAME", "COUCH",
                "TABLE", "BED", "LAMP", "PLANT", "SCREEN", "STORAGE", "GLOBAL_MESH", "OTHER"
            }, MetaLabels.All.ToArray());
            Assert.AreEqual(16, MetaLabels.All.Count);
            Assert.IsTrue(MetaLabels.IsMetaLabel("WALL_ART"));
            Assert.IsFalse(MetaLabels.IsMetaLabel("wall_art"));
            Assert.IsFalse(MetaLabels.IsMetaLabel("CHAIR"));
            Assert.IsFalse(MetaLabels.IsMetaLabel(null));
        }

        [Test]
        public void EveryMetaLabelIsReachableFromSomeTag()
        {
            var produced = Vocabulary.Tags.Select(tag => Vocabulary.ToSemanticLabel(tag)).Distinct().ToArray();

            CollectionAssert.AreEquivalent(MetaLabels.All.ToArray(), produced);
        }

        // ---- synonyms the plan names ----

        [TestCase("sofa")]
        [TestCase("couch")]
        [TestCase("sofa bed")]
        [TestCase("loveseat")]
        [TestCase("Love Seat")]
        [TestCase("a sofa-bed")]
        public void CouchSynonyms_ShareOneTag(string raw)
        {
            Assert.AreEqual("couch", Vocabulary.Normalize(raw));
            Assert.AreEqual("COUCH", Vocabulary.ToSemanticLabel(raw));
        }

        // The documented choice: a TV and a monitor are both a screen, the word Meta, the scanner's Meta mapper and the
        // web Scan app share. The raw text ("TV screen") stays in the object's label text.
        [TestCase("tv")]
        [TestCase("TV")]
        [TestCase("tv screen")]
        [TestCase("television")]
        [TestCase("monitor")]
        [TestCase("computer monitor")]
        [TestCase("screen")]
        [TestCase("tvs")]
        public void ScreenSynonyms_ShareOneTag(string raw)
        {
            Assert.AreEqual("screen", Vocabulary.Normalize(raw));
            Assert.AreEqual("SCREEN", Vocabulary.ToSemanticLabel(raw));
        }

        // Meta has no label for these: OTHER, but each is a valid tag.
        [TestCase("chair")]
        [TestCase("cup")]
        [TestCase("laptop")]
        [TestCase("mug")]
        [TestCase("keyboard")]
        public void ThingsMetaHasNoLabelFor_AreOther_ButValidTags(string raw)
        {
            string tag = Vocabulary.Normalize(raw);

            Assert.AreEqual(raw, tag);
            Assert.IsTrue(Vocabulary.IsKnownTag(tag));
            Assert.AreEqual("OTHER", Vocabulary.ToSemanticLabel(raw));
        }

        // ---- the steps of Normalize ----

        [TestCase("TABLE", "table")]
        [TestCase("  TaBlE  ", "table")]
        [TestCase("table!", "table")]
        [TestCase("table?!", "table")]
        [TestCase("\ttable\r\n", "table")]
        [TestCase("floor_lamp", "lamp")]
        [TestCase("floor-lamp", "lamp")]
        [TestCase("FLOOR   LAMP", "lamp")]
        [TestCase("kid's chair", "kids chair")]
        [TestCase("Küche", "küche")]
        public void Normalize_FoldsCaseSpaceAndPunctuation(string raw, string tag)
        {
            Assert.AreEqual(tag, Vocabulary.Normalize(raw));
        }

        [TestCase("a couch", "couch")]
        [TestCase("an apple", "apple")]
        [TestCase("the table", "table")]
        [TestCase("The Table", "table")]
        [TestCase("the the lamp", "lamp")]
        [TestCase("a  the couch", "couch")]
        [TestCase("anchor", "anchor")]
        [TestCase("theater", "theater")]
        [TestCase("another lamp", "another lamp")]
        public void Normalize_DropsALeadingArticleOnly_NotAWordThatStartsWithOne(string raw, string tag)
        {
            Assert.AreEqual(tag, Vocabulary.Normalize(raw));
        }

        [TestCase("tables", "table")]
        [TestCase("boxes", "box")]
        [TestCase("benches", "bench")]
        [TestCase("dishes", "dish")]
        [TestCase("couches", "couch")]
        [TestCase("shelves", "shelf")]
        [TestCase("books", "book")]
        [TestCase("mattresses", "bed")]
        [TestCase("coffee tables", "table")]
        [TestCase("remote controls", "remote control")]
        [TestCase("batteries", "battery")]
        [TestCase("shoes", "shoe")]
        [TestCase("knives", "knife")]
        [TestCase("glasses", "glasses")]
        [TestCase("sunglasses", "sunglasses")]
        [TestCase("scissors", "scissors")]
        [TestCase("pants", "pants")]
        [TestCase("lens", "lens")]
        [TestCase("bus", "bus")]
        [TestCase("glass", "glass")]
        [TestCase("cactus", "cactus")]
        public void Normalize_SingularizesSimplePlurals_AndLeavesWordsThatAreNotPlurals(string raw, string tag)
        {
            Assert.AreEqual(tag, Vocabulary.Normalize(raw));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("   ")]
        [TestCase("\t")]
        [TestCase("\r\n")]
        [TestCase(" \t \n ")]
        [TestCase(".")]
        [TestCase("...")]
        [TestCase("---")]
        [TestCase("_")]
        [TestCase("!!!")]
        [TestCase("123")]
        [TestCase("1.5")]
        [TestCase("a")]
        [TestCase("an")]
        [TestCase("the")]
        [TestCase("a 1")]
        [TestCase("( )")]
        public void Garbage_GivesAnEmptyTag_AndOther(string raw)
        {
            Assert.AreEqual(string.Empty, Vocabulary.Normalize(raw));
            Assert.AreEqual("OTHER", Vocabulary.ToSemanticLabel(raw));
            Assert.IsFalse(Vocabulary.IsKnownTag(Vocabulary.Normalize(raw)));
        }

        [TestCase("3d printer", "3d printer")]
        [TestCase("a5 paper", "a5 paper")]
        public void TextWithALetter_KeepsItsDigits(string raw, string tag)
        {
            Assert.AreEqual(tag, Vocabulary.Normalize(raw));
        }

        [Test]
        public void Normalize_IsIdempotent_AndGivesTheSameResultEachTime()
        {
            var inputs = new[] { "TV screen", "Sofa Bed", "tables", "a couch", "unicorns", "door  frame", "chest of drawers", "Küche", "123", "" };

            foreach (var input in inputs)
            {
                string once = Vocabulary.Normalize(input);

                Assert.AreEqual(once, Vocabulary.Normalize(input), input);
                Assert.AreEqual(once, Vocabulary.Normalize(once), "idempotent for " + input);
                Assert.AreEqual(Vocabulary.ToSemanticLabel(input), Vocabulary.ToSemanticLabel(once), "same meaning for " + input);
            }
        }

        // Upper-casing and lower-casing use the invariant culture: in Turkish an I lower-cases to a dotless i.
        [Test]
        public void Normalize_DoesNotDependOnTheCurrentCulture()
        {
            CultureInfo turkish;
            try
            {
                turkish = new CultureInfo("tr-TR");
            }
            catch (CultureNotFoundException)
            {
                return; // not available on this machine: nothing to check
            }

            var saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = turkish;
                Assert.AreEqual("invisible wall", Vocabulary.Normalize("INVISIBLE_WALL_FACE"));
                Assert.AreEqual("INVISIBLE_WALL_FACE", Vocabulary.ToSemanticLabel("INVISIBLE_WALL_FACE"));
                Assert.AreEqual("SCREEN", Vocabulary.ToSemanticLabel("TV"));
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        // ---- the table itself ----

        [Test]
        public void EveryEntry_HasACleanLowerCaseTag_ThatNormalizesToItself()
        {
            foreach (var entry in TagVocabulary.DefaultEntries)
            {
                Assert.AreEqual(entry.Tag, Vocabulary.Normalize(entry.Tag), "the tag \"" + entry.Tag + "\" must be canonical");
                Assert.AreEqual(entry.Tag, entry.Tag.ToLowerInvariant());
                Assert.IsTrue(MetaLabels.IsMetaLabel(entry.Semantic), entry.Tag + " has the label " + entry.Semantic);
            }
        }

        [Test]
        public void EverySynonym_NormalizesToItsEntrysTag()
        {
            foreach (var entry in TagVocabulary.DefaultEntries)
            {
                foreach (var synonym in entry.Synonyms)
                {
                    Assert.AreEqual(entry.Tag, Vocabulary.Normalize(synonym), "synonym \"" + synonym + "\" of \"" + entry.Tag + "\"");
                    Assert.AreEqual(entry.Semantic, Vocabulary.ToSemanticLabel(synonym));
                }
            }
        }

        [Test]
        public void NoTagOrSynonymBelongsToTwoEntries()
        {
            var seen = new Dictionary<string, string>();
            foreach (var entry in TagVocabulary.DefaultEntries)
            {
                foreach (var phrase in new[] { entry.Tag }.Concat(entry.Synonyms))
                {
                    string key = phrase.ToLowerInvariant();
                    Assert.IsFalse(seen.ContainsKey(key), "\"" + phrase + "\" is listed under \"" + entry.Tag + "\" and \"" + (seen.ContainsKey(key) ? seen[key] : "") + "\"");
                    seen[key] = entry.Tag;
                }
            }
        }

        [Test]
        public void Tags_ListsEveryEntryOnce_InTheOrderOfTheTable()
        {
            CollectionAssert.AreEqual(TagVocabulary.DefaultEntries.Select(entry => entry.Tag).ToArray(), Vocabulary.Tags.ToArray());
            Assert.AreEqual(Vocabulary.Tags.Count, Vocabulary.Tags.Distinct().Count());
        }

        // ---- a vocabulary of one's own ----

        [Test]
        public void ACustomVocabulary_UsesItsOwnEntries()
        {
            var custom = new TagVocabulary(new[]
            {
                new TagEntry("widget", "OTHER", "gizmo", "thingamajig"),
                new TagEntry("couch", "COUCH")
            });

            Assert.AreEqual("widget", custom.Normalize("Gizmos"));
            Assert.AreEqual("widget", custom.Normalize("a thingamajig."));
            Assert.AreEqual("sofa", custom.Normalize("sofa"), "sofa is not a synonym here");
            Assert.AreEqual("COUCH", custom.ToSemanticLabel("couch"));
            Assert.AreEqual("OTHER", custom.ToSemanticLabel("sofa"));
            Assert.IsTrue(custom.IsKnownTag("widget"));
            Assert.IsFalse(custom.IsKnownTag("chair"));
        }

        [Test]
        public void ACustomVocabulary_RejectsABrokenTable()
        {
            Assert.Throws<ArgumentException>(() => new TagVocabulary(new[] { new TagEntry("widget", "NOT_A_LABEL") }), "unknown Meta label");
            Assert.Throws<ArgumentException>(() => new TagVocabulary(new[] { new TagEntry("widget", "OTHER", "gizmo"), new TagEntry("gadget", "OTHER", "gizmo") }), "one synonym under two tags");
            Assert.Throws<ArgumentException>(() => new TagVocabulary(new[] { new TagEntry("widget", "OTHER"), new TagEntry("Widget", "TABLE") }), "one tag twice");
            Assert.Throws<ArgumentException>(() => new TagVocabulary(new[] { new TagEntry("  ", "OTHER") }), "no tag");
            Assert.Throws<ArgumentException>(() => new TagVocabulary(new[] { new TagEntry("123", "OTHER") }), "a tag that is garbage");
            Assert.Throws<ArgumentException>(() => new TagVocabulary(new[] { new TagEntry("widgets", "OTHER") }), "a plural tag");
            Assert.Throws<ArgumentNullException>(() => new TagVocabulary(null));
        }
    }
}
