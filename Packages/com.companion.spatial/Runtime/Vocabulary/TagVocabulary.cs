using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Companion.Spatial.Vocabulary
{
    /// <summary>
    /// The one tag vocabulary of the scanner. Raw detector text (GroundingDINO, Florence, Meta Space Setup labels) goes
    /// to a canonical lower-case singular tag with <see cref="Normalize"/>, and a tag goes to one of Meta's 16 semantic
    /// labels with <see cref="ToSemanticLabel"/>. It replaces <c>SemanticLabelMapper</c>, which only matched exact keys:
    /// "chair", "picture frame", "a couch", "tables", "TV screen", "Sofa Bed", "door  frame" and "couch." gave OTHER.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Normalize"/> works in steps, always the same way for the same text: lower-case (invariant culture);
    /// every run of anything that is not a letter or digit becomes one space, so "door_frame", "door-frame" and
    /// "door  frame." read alike; apostrophes are dropped; a leading "a", "an" or "the" is dropped; text with no letter
    /// left ("123", ".", "a") is garbage and gives an empty tag; then the table is asked for the whole phrase, then for
    /// the phrase with its last word made singular ("coffee tables" becomes "coffee table"). A phrase the table does
    /// not know is its own tag (singularized): "unicorns" gives "unicorn" and maps to OTHER.
    /// </para>
    /// <para>
    /// The vocabulary does not guess from the last word of an unknown phrase: "wall art frame" is its own tag. Add the
    /// phrase to the table (see <c>DefaultTags</c>) when it should mean something.
    /// </para>
    /// </remarks>
    public sealed class TagVocabulary
    {
        private readonly Dictionary<string, string> _tagByPhrase = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _semanticByTag = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> _tags = new List<string>();
        private readonly ReadOnlyCollection<string> _tagsView;

        /// <summary>
        /// Builds a vocabulary from rows. Each tag is cleaned like any text; it must keep something after cleaning, be
        /// singular, and be listed once. Each row's Meta label must be one of <see cref="MetaLabels.All"/>. A tag or
        /// synonym may not appear in two rows.
        /// </summary>
        /// <param name="entries">The rows, in the order <see cref="Tags"/> should list them.</param>
        /// <exception cref="ArgumentNullException"><paramref name="entries"/> is null.</exception>
        /// <exception cref="ArgumentException">A row is broken, or two rows claim the same phrase.</exception>
        public TagVocabulary(IEnumerable<TagEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            _tagsView = new ReadOnlyCollection<string>(_tags);
            foreach (TagEntry entry in entries)
            {
                if (entry == null)
                {
                    throw new ArgumentException("The table has a null row.", nameof(entries));
                }

                string tag = TagText.Clean(entry.Tag);
                if (tag.Length == 0)
                {
                    throw new ArgumentException("The tag \"" + entry.Tag + "\" has no letter in it.", nameof(entries));
                }

                if (TagText.SingularizeLastWord(tag) != tag)
                {
                    throw new ArgumentException("The tag \"" + entry.Tag + "\" is not singular.", nameof(entries));
                }

                if (!MetaLabels.IsMetaLabel(entry.Semantic))
                {
                    throw new ArgumentException("The tag \"" + entry.Tag + "\" has \"" + entry.Semantic + "\", which is not one of Meta's 16 labels.", nameof(entries));
                }

                Claim(tag, tag);
                foreach (string synonym in entry.Synonyms)
                {
                    string phrase = TagText.Clean(synonym);
                    if (phrase.Length == 0)
                    {
                        throw new ArgumentException("The synonym \"" + synonym + "\" of \"" + entry.Tag + "\" has no letter in it.", nameof(entries));
                    }

                    Claim(phrase, tag);
                }

                _semanticByTag[tag] = entry.Semantic;
                _tags.Add(tag);
            }
        }

        /// <summary>The scanner's vocabulary: the table in <c>DefaultTags</c>.</summary>
        public static TagVocabulary Default { get; } = new TagVocabulary(DefaultTags.Entries);

        /// <summary>The rows of the default table, in table order.</summary>
        public static IReadOnlyList<TagEntry> DefaultEntries
        {
            get { return DefaultTags.Entries; }
        }

        /// <summary>Every canonical tag this vocabulary knows, in table order.</summary>
        public IReadOnlyList<string> Tags
        {
            get { return _tagsView; }
        }

        /// <summary>
        /// The canonical tag for raw text: lower case, singular, one space between words, synonyms folded together
        /// ("sofa", "Sofa Bed" and "a loveseat" are all "couch"; "TV screen" and "monitor" are "screen"). An empty
        /// string for null, empty and garbage text. Text the table does not know is returned as its own tag.
        /// </summary>
        /// <param name="raw">Detector text, a Meta label such as "WALL_ART", or an existing tag.</param>
        public string Normalize(string raw)
        {
            string phrase = TagText.Clean(raw);
            if (phrase.Length == 0)
            {
                return phrase;
            }

            string tag;
            if (_tagByPhrase.TryGetValue(phrase, out tag))
            {
                return tag;
            }

            string singular = TagText.SingularizeLastWord(phrase);
            if (_tagByPhrase.TryGetValue(singular, out tag))
            {
                return tag;
            }

            return singular;
        }

        /// <summary>
        /// The Meta label (one of <see cref="MetaLabels.All"/>) for raw text or a canonical tag: the Meta label of its
        /// tag, or <see cref="MetaLabels.Other"/> for an unknown tag and for garbage. The 16 labels give themselves
        /// back ("WALL_FACE" gives "WALL_FACE"), including INVISIBLE_WALL_FACE and GLOBAL_MESH.
        /// </summary>
        /// <param name="rawOrCanonical">Detector text, a Meta label, or a canonical tag.</param>
        public string ToSemanticLabel(string rawOrCanonical)
        {
            string semantic;
            return _semanticByTag.TryGetValue(Normalize(rawOrCanonical), out semantic) ? semantic : MetaLabels.Other;
        }

        /// <summary>
        /// True when <paramref name="tag"/> is exactly one of this vocabulary's canonical tags, including the ones that
        /// map to OTHER ("chair"). An unknown tag and the empty tag are not known.
        /// </summary>
        /// <param name="tag">A canonical tag, as <see cref="Normalize"/> returns it.</param>
        public bool IsKnownTag(string tag)
        {
            return tag != null && _semanticByTag.ContainsKey(tag);
        }

        private void Claim(string phrase, string tag)
        {
            string owner;
            if (_tagByPhrase.TryGetValue(phrase, out owner))
            {
                throw new ArgumentException("\"" + phrase + "\" is listed twice: for \"" + owner + "\" and for \"" + tag + "\".");
            }

            _tagByPhrase[phrase] = tag;
        }
    }
}
