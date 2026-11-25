using System.Text;

namespace Companion.Spatial.Vocabulary
{
    /// <summary>
    /// The text rules of <see cref="TagVocabulary.Normalize"/>, with no table in them: cleaning a phrase (case, white
    /// space, punctuation, a leading article) and singularizing its last word. They hold no state, so the same text
    /// always gives the same result.
    /// </summary>
    internal static class TagText
    {
        /// <summary>
        /// Lower-cases the text, turns every run of anything that is not a letter or a digit (white space, "_", "-", ".",
        /// "," and so on) into one space, drops apostrophes ("kid's" becomes "kids"), trims, and drops leading articles
        /// ("a", "an", "the"). Returns an empty string for null, for text with no letter left (digits and punctuation
        /// only) and for a lone article.
        /// </summary>
        internal static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var text = new StringBuilder(raw.Length);
            bool pendingSpace = false;
            foreach (char c in raw)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (pendingSpace && text.Length > 0)
                    {
                        text.Append(' ');
                    }

                    pendingSpace = false;
                    text.Append(char.ToLowerInvariant(c));
                }
                else if (c == '\'' || c == '\u2019' || c == '\u02BC')
                {
                    // an apostrophe joins its neighbours: it is neither a letter nor a separator
                }
                else
                {
                    pendingSpace = true;
                }
            }

            string phrase = DropArticles(text.ToString());
            return HasLetter(phrase) ? phrase : string.Empty;
        }

        /// <summary>
        /// Trims the text and turns every run of white space into one space, leaving case and punctuation alone: the
        /// way a label is shown ("TV  screen " becomes "TV screen"). Null gives an empty string.
        /// </summary>
        internal static string Collapse(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var text = new StringBuilder(raw.Length);
            bool pendingSpace = false;
            foreach (char c in raw)
            {
                if (char.IsWhiteSpace(c))
                {
                    pendingSpace = true;
                    continue;
                }

                if (pendingSpace && text.Length > 0)
                {
                    text.Append(' ');
                }

                pendingSpace = false;
                text.Append(c);
            }

            return text.ToString();
        }

        /// <summary>
        /// Singularizes the last word of a clean phrase: "coffee tables" becomes "coffee table". Words that only look
        /// plural ("glass", "lens", "cactus") and words that are plural by name ("glasses", "scissors", "pants") stay.
        /// </summary>
        internal static string SingularizeLastWord(string phrase)
        {
            int space = phrase.LastIndexOf(' ');
            if (space < 0)
            {
                return SingularWord(phrase);
            }

            return phrase.Substring(0, space + 1) + SingularWord(phrase.Substring(space + 1));
        }

        private static string DropArticles(string phrase)
        {
            while (true)
            {
                int space = phrase.IndexOf(' ');
                string first = space < 0 ? phrase : phrase.Substring(0, space);
                if (!IsArticle(first))
                {
                    return phrase;
                }

                phrase = space < 0 ? string.Empty : phrase.Substring(space + 1);
            }
        }

        private static bool IsArticle(string word)
        {
            return word == "a" || word == "an" || word == "the";
        }

        private static bool HasLetter(string phrase)
        {
            foreach (char c in phrase)
            {
                if (char.IsLetter(c))
                {
                    return true;
                }
            }

            return false;
        }

        private static string SingularWord(string word)
        {
            string irregular = IrregularSingular(word);
            if (irregular != null)
            {
                return irregular;
            }

            if (word.Length < 4 || word[word.Length - 1] != 's' || EndsPlural(word) || EndsLikeASingular(word))
            {
                return word;
            }

            if (word.EndsWith("ies", System.StringComparison.Ordinal) && word.Length > 4)
            {
                return word.Substring(0, word.Length - 3) + "y";
            }

            if (word.EndsWith("sses", System.StringComparison.Ordinal) ||
                word.EndsWith("ches", System.StringComparison.Ordinal) ||
                word.EndsWith("shes", System.StringComparison.Ordinal) ||
                word.EndsWith("xes", System.StringComparison.Ordinal) ||
                word.EndsWith("zes", System.StringComparison.Ordinal))
            {
                return word.Substring(0, word.Length - 2);
            }

            return word.Substring(0, word.Length - 1);
        }

        // Words that end in "ss", "us" or "is" are not plurals: glass, mattress, cactus, iris.
        private static bool EndsLikeASingular(string word)
        {
            return word.EndsWith("ss", System.StringComparison.Ordinal) ||
                   word.EndsWith("us", System.StringComparison.Ordinal) ||
                   word.EndsWith("is", System.StringComparison.Ordinal);
        }

        // A word that is already the singular, or that has no singular: it keeps its "s".
        private static bool EndsPlural(string word)
        {
            switch (word)
            {
                case "lens":
                case "canvas":
                case "series":
                case "species":
                case "news":
                case "bias":
                case "atlas":
                case "glasses":
                case "eyeglasses":
                case "sunglasses":
                case "scissors":
                case "pants":
                case "jeans":
                case "shorts":
                case "trousers":
                case "stairs":
                case "binoculars":
                    return true;
                default:
                    return false;
            }
        }

        // Plurals the rules would get wrong. Add a word here when the generic rules fail for it.
        private static string IrregularSingular(string word)
        {
            switch (word)
            {
                case "shelves": return "shelf";
                case "leaves": return "leaf";
                case "knives": return "knife";
                case "loaves": return "loaf";
                case "halves": return "half";
                case "wolves": return "wolf";
                case "scarves": return "scarf";
                case "wives": return "wife";
                case "lives": return "life";
                case "mice": return "mouse";
                case "feet": return "foot";
                case "teeth": return "tooth";
                case "men": return "man";
                case "women": return "woman";
                case "children": return "child";
                case "people": return "person";
                case "cookies": return "cookie";
                case "movies": return "movie";
                case "tvs": return "tv";
                case "pcs": return "pc";
                default: return null;
            }
        }
    }
}
