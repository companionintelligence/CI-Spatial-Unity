using Companion.Spatial.Vocabulary;

namespace Companion.Spatial.Extras
{
    /// <summary>
    /// What an object is called, in the three layers of the tag vocabulary: the text a detector or the headset gave,
    /// its canonical tag, and the Meta label the tag maps to. The v1 extras had only the first (<c>label</c>) and a
    /// second (<c>semantic_label</c>) that was <c>OTHER</c> for most things.
    /// </summary>
    public sealed class ScanLabel : ExtrasNode
    {
        /// <summary>Creates an empty label: no text, no tag, OTHER.</summary>
        public ScanLabel()
        {
            Text = string.Empty;
            Tag = string.Empty;
            Semantic = MetaLabels.Other;
        }

        /// <summary>Creates a label from its three layers.</summary>
        /// <param name="text">The text as shown ("TV screen").</param>
        /// <param name="tag">The canonical tag ("screen").</param>
        /// <param name="semantic">One of the 16 Meta labels ("SCREEN").</param>
        public ScanLabel(string text, string tag, string semantic)
        {
            Text = text ?? string.Empty;
            Tag = tag ?? string.Empty;
            Semantic = semantic ?? MetaLabels.Other;
        }

        /// <summary>The text a detector or the headset gave, as it should be shown (JSON <c>text</c>).</summary>
        public string Text { get; set; }

        /// <summary>The canonical lower-case singular tag of the text (JSON <c>tag</c>), as <see cref="TagVocabulary.Normalize"/> gives it.</summary>
        public string Tag { get; set; }

        /// <summary>One of the 16 Meta labels (JSON <c>semantic</c>, see <see cref="MetaLabels"/>): what Meta's Space Setup would call the object.</summary>
        public string Semantic { get; set; }

        /// <summary>
        /// Makes a label from raw text with a vocabulary: the text trimmed, its tag and its Meta label.
        /// </summary>
        /// <param name="text">The raw text ("TV screen").</param>
        /// <param name="vocabulary">The vocabulary to ask; null means <see cref="TagVocabulary.Default"/>.</param>
        public static ScanLabel FromText(string text, TagVocabulary vocabulary = null)
        {
            TagVocabulary words = vocabulary ?? TagVocabulary.Default;
            return new ScanLabel(TagText.Collapse(text), words.Normalize(text), words.ToSemanticLabel(text));
        }
    }
}
