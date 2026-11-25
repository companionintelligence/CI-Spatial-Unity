using System.Collections.Generic;
using System.Linq;
using System.Text;
using CompanionScan.Mapping;
using NUnit.Framework;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// <see cref="SemanticLabelMapper"/> as the scanner uses it: raw detector text in, one of Meta's
    /// sixteen labels out. The words themselves are decided by the tag vocabulary of the
    /// Companion.Spatial package, whose own tests cover the whole table; these tests are the
    /// scanner's side of it. The mapper used to look the text up whole and gave OTHER to a chair,
    /// "a couch", "tables", "TV screen", "Sofa Bed", "door  frame" and "couch.", and to 16 of the
    /// 26 classes of the default prompt; INVISIBLE_WALL_FACE and GLOBAL_MESH could not be produced.
    /// </summary>
    public class SemanticLabelMapperCharacterizationTests
    {
        // Every key of the old exact-match map and the Meta label it gave: none of them changed.
        private static readonly (string raw, string expected)[] KnownLabels =
        {
            ("couch", "COUCH"),
            ("sofa", "COUCH"),
            ("bed", "BED"),
            ("table", "TABLE"),
            ("desk", "TABLE"),
            ("lamp", "LAMP"),
            ("plant", "PLANT"),
            ("screen", "SCREEN"),
            ("tv", "SCREEN"),
            ("monitor", "SCREEN"),
            ("storage", "STORAGE"),
            ("cabinet", "STORAGE"),
            ("shelf", "STORAGE"),
            ("door", "DOOR_FRAME"),
            ("door_frame", "DOOR_FRAME"),
            ("window", "WINDOW_FRAME"),
            ("window_frame", "WINDOW_FRAME"),
            ("wall", "WALL_FACE"),
            ("wall_face", "WALL_FACE"),
            ("ceiling", "CEILING"),
            ("floor", "FLOOR"),
            ("wall_art", "WALL_ART"),
            ("other", "OTHER")
        };

        private static IEnumerable<TestCaseData> KnownLabelCases()
        {
            foreach (var (raw, expected) in KnownLabels)
            {
                yield return new TestCaseData(raw, expected).SetName($"MapToMetaLabel_Maps_{raw}_To_{expected}");
            }
        }

        [TestCaseSource(nameof(KnownLabelCases))]
        public void MapToMetaLabel_StillGivesTheLabelOfEveryKeyOfTheOldMap(string raw, string expected)
        {
            Assert.AreEqual(expected, SemanticLabelMapper.MapToMetaLabel(raw));
        }

        [Test]
        public void MapToMetaLabel_IgnoresCase()
        {
            AssertAllMapTo("COUCH", "COUCH", "Couch", "cOUCH", "SoFa", "SOFA");
            AssertAllMapTo("SCREEN", "TV", "Tv", "tV", "MONITOR", "Screen");
            AssertAllMapTo("DOOR_FRAME", "DOOR_FRAME", "Door_Frame");
        }

        [Test]
        public void MapToMetaLabel_IgnoresWhitespaceAroundTheLabel()
        {
            AssertAllMapTo("COUCH", " couch", "couch ", "  couch  ", "\tcouch\n", "\r\n couch \r\n", " couch ");
            AssertAllMapTo("WALL_ART", "  wall art  ", " Wall-Art\t");
        }

        [Test]
        public void MapToMetaLabel_TreatsASpaceOrAHyphenLikeAnUnderscore()
        {
            AssertAllMapTo("DOOR_FRAME", "door frame", "door-frame", "door_frame", "Door Frame", "DOOR-FRAME", "door_frame ");
            AssertAllMapTo("WINDOW_FRAME", "window frame", "window-frame", "Window_Frame");
            AssertAllMapTo("WALL_ART", "wall art", "wall-art", "Wall Art", "WALL_ART");
            AssertAllMapTo("WALL_FACE", "wall face", "wall-face");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void MapToMetaLabel_ReturnsOtherWhenThereIsNoLabel(string raw)
        {
            Assert.AreEqual("OTHER", SemanticLabelMapper.MapToMetaLabel(raw));
        }

        [Test]
        public void MapToMetaLabel_ReturnsOtherForEveryKindOfWhitespace()
        {
            AssertAllMapTo("OTHER", "\t", "\r\n", " ", " ", " \t \n ");
        }

        [Test]
        public void MapToMetaLabel_ReturnsOtherForTextThatIsNotALabel()
        {
            AssertAllMapTo("OTHER", "unicorn", "x", "123", "couchh", "ouch");
        }

        // Meta has no label for a chair: OTHER, as the table of the vocabulary says. It is a tag of its own, which tracking uses.
        [Test]
        public void MapToMetaLabel_GivesAChairOther_BecauseMetaHasNoLabelForIt_ButItIsATag()
        {
            AssertAllMapTo("OTHER", "chair", "Chair", "CHAIR", " chair ", "armchair", "office chair");

            Assert.AreEqual("chair", SemanticLabelMapper.NormalizeTag("Armchair"));
            Assert.AreEqual("chair", SemanticLabelMapper.NormalizeTag("an office chair"));
            Assert.AreNotEqual(SemanticLabelMapper.NormalizeTag("cup"), SemanticLabelMapper.NormalizeTag("chair"), "a cup is not a chair, though both are OTHER");
        }

        [Test]
        public void MapToMetaLabel_DropsAnArticle()
        {
            AssertAllMapTo("COUCH", "a couch", "A Couch");
            AssertAllMapTo("TABLE", "the table");
            AssertAllMapTo("LAMP", "A Lamp", "an lamp");
        }

        [Test]
        public void MapToMetaLabel_RecognisesAPlural()
        {
            AssertAllMapTo("TABLE", "tables");
            AssertAllMapTo("COUCH", "sofas");
            AssertAllMapTo("WINDOW_FRAME", "windows");
            AssertAllMapTo("DOOR_FRAME", "doors");
            AssertAllMapTo("PLANT", "plants");
            AssertAllMapTo("STORAGE", "shelves");
            AssertAllMapTo("LAMP", "lamps");
        }

        [Test]
        public void MapToMetaLabel_KnowsTheUsualCompounds()
        {
            AssertAllMapTo("SCREEN", "TV screen", "tv", "television");
            AssertAllMapTo("COUCH", "Sofa Bed", "loveseat");
            AssertAllMapTo("TABLE", "coffee table", "dining table");
            AssertAllMapTo("LAMP", "floor lamp", "desk lamp");
            AssertAllMapTo("WALL_ART", "picture frame", "painting", "poster");
            AssertAllMapTo("STORAGE", "bookshelf", "cupboard", "wardrobe");
        }

        // The vocabulary does not guess from the last word: a phrase it does not list is its own tag, and OTHER.
        [Test]
        public void MapToMetaLabel_DoesNotGuessAnUnlistedCompoundFromItsLastWord()
        {
            AssertAllMapTo("OTHER", "wall art frame", "couch sofa", "unicorn lamp");
        }

        [Test]
        public void MapToMetaLabel_MatchesARepeatedOrMixedSeparator()
        {
            AssertAllMapTo("DOOR_FRAME", "door  frame", "door - frame", "door_ frame");
            AssertAllMapTo("WALL_ART", "wall--art");
            AssertAllMapTo("COUCH", "-couch", "couch-", "_couch_");
        }

        [Test]
        public void MapToMetaLabel_IgnoresATabOrPunctuationAroundTheLabel()
        {
            AssertAllMapTo("DOOR_FRAME", "door\tframe");
            AssertAllMapTo("COUCH", "couch.", "couch ,", "couch .", "(couch)");
        }

        // INVISIBLE_WALL_FACE and GLOBAL_MESH are supported labels that no text could produce, not even their own name.
        [Test]
        public void MapToMetaLabel_CanGiveInvisibleWallFaceAndGlobalMesh()
        {
            AssertAllMapTo("INVISIBLE_WALL_FACE", "INVISIBLE_WALL_FACE", "invisible wall face", "invisible-wall-face");
            AssertAllMapTo("GLOBAL_MESH", "GLOBAL_MESH", "global mesh");
        }

        [Test]
        public void MapToMetaLabel_OnlyEverGivesASupportedMetaLabel()
        {
            var texts = KnownLabels.Select(label => label.raw)
                .Concat(DefaultPromptClasses())
                .Concat(new[] { null, "", "  ", "unicorn", "123", ".", "couch/sofa", "Küche", "a", "the the", new string('x', 500) })
                .ToArray();

            foreach (var text in texts)
            {
                CollectionAssert.Contains(SemanticLabelMapper.SupportedMetaLabels, SemanticLabelMapper.MapToMetaLabel(text),
                    $"label for {Show(text)}");
            }
        }

        [Test]
        public void SupportedMetaLabels_AreTheSixteenLabelsOfTheSpaceSetupExportInThisOrder()
        {
            CollectionAssert.AreEqual(new[]
            {
                "CEILING", "DOOR_FRAME", "FLOOR", "INVISIBLE_WALL_FACE", "WALL_ART", "WALL_FACE", "WINDOW_FRAME", "COUCH",
                "TABLE", "BED", "LAMP", "PLANT", "SCREEN", "STORAGE", "GLOBAL_MESH", "OTHER"
            }, SemanticLabelMapper.SupportedMetaLabels);
        }

        // Fed its own labels (a Space Setup read gives them), the mapper gives all 16 back unchanged.
        [Test]
        public void MapToMetaLabel_ReturnsEveryOneOfTheSupportedLabelsUnchanged()
        {
            var changed = SemanticLabelMapper.SupportedMetaLabels
                .Where(label => SemanticLabelMapper.MapToMetaLabel(label) != label)
                .ToArray();

            CollectionAssert.IsEmpty(changed);
        }

        // 9 of the 26 default-prompt classes get a Meta label (10 with the picture frame, now wall art) and 16 are OTHER because
        // Meta has no label for them; update the list when the prompt changes.
        [Test]
        public void DefaultGroundingDinoPromptClasses_MapAsListed()
        {
            CollectionAssert.AreEqual(new[]
            {
                "chair=OTHER", "table=TABLE", "sofa=COUCH", "bed=BED", "lamp=LAMP", "cup=OTHER", "mug=OTHER",
                "bottle=OTHER", "bowl=OTHER", "book=OTHER", "laptop=OTHER", "monitor=SCREEN", "keyboard=OTHER",
                "phone=OTHER", "remote control=OTHER", "plant=PLANT", "vase=OTHER", "clock=OTHER",
                "picture frame=WALL_ART", "shelf=STORAGE", "cabinet=STORAGE", "door=DOOR_FRAME",
                "window=WINDOW_FRAME", "box=OTHER", "bag=OTHER", "pillow=OTHER"
            }, DefaultPromptClasses().Select(item => item + "=" + SemanticLabelMapper.MapToMetaLabel(item)).ToArray());
        }

        [Test]
        public void EveryDefaultPromptClass_IsATag_SoTrackingCanTellThemApart()
        {
            var tags = DefaultPromptClasses().Select(SemanticLabelMapper.NormalizeTag).ToArray();

            CollectionAssert.DoesNotContain(tags, string.Empty);
            // "monitor" and "sofa" share a tag with "screen" and "couch": synonyms a detector swaps for each other are one object.
            Assert.AreEqual("screen", SemanticLabelMapper.NormalizeTag("monitor"));
            Assert.AreEqual("couch", SemanticLabelMapper.NormalizeTag("sofa"));
            Assert.AreEqual(26, tags.Distinct().Count(), "two classes of the default prompt share a tag, so tracking would take one object for the other");
        }

        private static string[] DefaultPromptClasses()
        {
            return SpatialCapturePipeline.DefaultGroundingDinoPrompt
                .Split('.')
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToArray();
        }

        /// <summary>Checks every label and names all those that do not give <paramref name="expected"/>.</summary>
        private static void AssertAllMapTo(string expected, params string[] raws)
        {
            var wrong = raws
                .Where(raw => SemanticLabelMapper.MapToMetaLabel(raw) != expected)
                .Select(raw => $"{Show(raw)} gave {SemanticLabelMapper.MapToMetaLabel(raw)}")
                .ToList();

            Assert.IsEmpty(wrong, $"each of these should give {expected}: " + string.Join(", ", wrong));
        }

        private static string Show(string raw)
        {
            if (raw == null)
            {
                return "null";
            }

            var text = new StringBuilder("\"");
            foreach (var c in raw)
            {
                if (c < ' ' || c > '~')
                {
                    text.Append("\\u").Append(((int)c).ToString("X4"));
                }
                else
                {
                    text.Append(c);
                }
            }

            return text.Append('"').ToString();
        }
    }
}
