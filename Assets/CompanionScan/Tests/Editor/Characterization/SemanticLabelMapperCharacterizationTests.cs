using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CompanionScan.Mapping;
using NUnit.Framework;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// Characterization tests: they record what <see cref="SemanticLabelMapper"/> does today, so a
    /// refactor cannot change it unnoticed. A test with "Currently" in its name records a result
    /// that looks wrong or accidental. It is not a requirement: change it on purpose, together
    /// with the code, once the result has been decided.
    /// </summary>
    public class SemanticLabelMapperCharacterizationTests
    {
        // Every key of the map and the Meta label it gives, in the order of the source.
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
        public void MapToMetaLabel_GivesTheListedLabelForEveryKeyOfTheMap(string raw, string expected)
        {
            Assert.AreEqual(expected, SemanticLabelMapper.MapToMetaLabel(raw));
        }

        [Test]
        public void TheMap_HasExactlyTheKeysListedInThisFile()
        {
            var keys = ReadMap().Keys.OrderBy(key => key).ToArray();
            var expected = KnownLabels.Select(label => label.raw).OrderBy(key => key).ToArray();

            Assert.AreEqual(23, expected.Length, "the list in this file changed");
            CollectionAssert.AreEqual(expected, keys, "a key was added or removed: give it a test above");
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
            AssertAllMapTo("COUCH", " couch", "couch ", "  couch  ", "\tcouch\n", "\r\n couch \r\n", " couch ");
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
            AssertAllMapTo("OTHER", "\t", "\r\n", " ", " ", " \t \n ");
        }

        [Test]
        public void MapToMetaLabel_ReturnsOtherForALabelThatIsNotInTheMap()
        {
            AssertAllMapTo("OTHER", "unicorn", "x", "123", "couchh", "ouch");
        }

        // "chair" leads the default GroundingDINO prompt and is furniture, yet the map has no entry for it.
        [Test]
        public void MapToMetaLabel_Currently_MapsAChairToOther()
        {
            AssertAllMapTo("OTHER", "chair", "Chair", "CHAIR", " chair ", "armchair", "office chair");
        }

        // The label is looked up whole: an article stays part of it.
        [Test]
        public void MapToMetaLabel_Currently_DoesNotDropAnArticle()
        {
            AssertAllMapTo("OTHER", "a couch", "the table", "an armchair", "A Lamp");
        }

        // The label is looked up as written: a plural is another word.
        [Test]
        public void MapToMetaLabel_Currently_DoesNotRecogniseAPlural()
        {
            AssertAllMapTo("OTHER", "tables", "sofas", "windows", "doors", "plants", "shelves", "lamps");
        }

        // The label is not split into words: a compound is OTHER even when its words are listed.
        [Test]
        public void MapToMetaLabel_Currently_DoesNotMapACompoundByItsWords()
        {
            AssertAllMapTo("OTHER", "TV screen", "Sofa Bed", "coffee table", "floor lamp", "wall art frame",
                "picture frame", "bookshelf", "desk lamp");
        }

        // Each space or hyphen becomes one underscore, so a doubled or mixed separator never matches.
        [Test]
        public void MapToMetaLabel_Currently_DoesNotMatchARepeatedOrMixedSeparator()
        {
            AssertAllMapTo("OTHER", "door  frame", "door - frame", "door_ frame", "wall--art", "-couch", "couch-", "_couch_");
        }

        // A tab inside the label, and punctuation after it, are kept: "couch." is not "couch".
        [Test]
        public void MapToMetaLabel_Currently_KeepsATabOrPunctuationInsideTheLabel()
        {
            AssertAllMapTo("OTHER", "door\tframe", "couch.", "couch ,", "couch .", "(couch)", "couch/sofa");
        }

        // INVISIBLE_WALL_FACE and GLOBAL_MESH are supported labels, but no text maps to them, not even their own name.
        [Test]
        public void MapToMetaLabel_Currently_NeverGivesInvisibleWallFaceOrGlobalMesh()
        {
            AssertAllMapTo("OTHER", "INVISIBLE_WALL_FACE", "invisible wall face", "invisible-wall-face", "GLOBAL_MESH", "global mesh");

            var produced = ReadMap().Values.Distinct().ToArray();
            CollectionAssert.AreEquivalent(new[] { "INVISIBLE_WALL_FACE", "GLOBAL_MESH" },
                SemanticLabelMapper.SupportedMetaLabels.Except(produced).ToArray());
        }

        [Test]
        public void MapToMetaLabel_OnlyEverGivesASupportedMetaLabel()
        {
            CollectionAssert.IsSubsetOf(ReadMap().Values.Distinct().ToArray(), SemanticLabelMapper.SupportedMetaLabels);
            CollectionAssert.Contains(SemanticLabelMapper.SupportedMetaLabels, SemanticLabelMapper.MapToMetaLabel("no such label"));
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

        // Fed its own labels, the mapper gives 14 of the 16 back unchanged.
        [Test]
        public void MapToMetaLabel_Currently_ReturnsFourteenOfTheSupportedLabelsUnchanged()
        {
            var changed = SemanticLabelMapper.SupportedMetaLabels
                .Where(label => SemanticLabelMapper.MapToMetaLabel(label) != label)
                .ToArray();

            CollectionAssert.AreEquivalent(new[] { "INVISIBLE_WALL_FACE", "GLOBAL_MESH" }, changed);
        }

        // 10 of the 26 default-prompt classes get a Meta label and 16 are OTHER; update the list when the prompt changes.
        [Test]
        public void DefaultGroundingDinoPromptClasses_Currently_MapAsListed()
        {
            var classes = SpatialCapturePipeline.DefaultGroundingDinoPrompt
                .Split('.')
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "chair=OTHER", "table=TABLE", "sofa=COUCH", "bed=BED", "lamp=LAMP", "cup=OTHER", "mug=OTHER",
                "bottle=OTHER", "bowl=OTHER", "book=OTHER", "laptop=OTHER", "monitor=SCREEN", "keyboard=OTHER",
                "phone=OTHER", "remote control=OTHER", "plant=PLANT", "vase=OTHER", "clock=OTHER",
                "picture frame=OTHER", "shelf=STORAGE", "cabinet=STORAGE", "door=DOOR_FRAME",
                "window=WINDOW_FRAME", "box=OTHER", "bag=OTHER", "pillow=OTHER"
            }, classes.Select(item => item + "=" + SemanticLabelMapper.MapToMetaLabel(item)).ToArray());
        }

        private static Dictionary<string, string> ReadMap()
        {
            var field = typeof(SemanticLabelMapper).GetField("Map", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "SemanticLabelMapper.Map was renamed or moved: update this test");
            return (Dictionary<string, string>)field.GetValue(null);
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
