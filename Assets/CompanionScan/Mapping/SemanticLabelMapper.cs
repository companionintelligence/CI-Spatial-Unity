using Companion.Spatial.Vocabulary;

namespace CompanionScan.Mapping
{
    /// <summary>
    /// Maps detector labels to Meta's semantic labels, through the scanner's one tag vocabulary
    /// (<see cref="TagVocabulary"/> in the Companion.Spatial package). The vocabulary reads the words
    /// the way people write them: case, articles, plurals, separators and the usual synonyms, so
    /// "chair" and "a couch", "tables", "TV screen", "Sofa Bed" and "door  frame" all find their label.
    /// This mapper used to look the text up whole, and gave OTHER to 16 of the 26 classes of the
    /// default prompt.
    /// </summary>
    public static class SemanticLabelMapper
    {
        public static readonly string[] SupportedMetaLabels =
        {
            "CEILING",
            "DOOR_FRAME",
            "FLOOR",
            "INVISIBLE_WALL_FACE",
            "WALL_ART",
            "WALL_FACE",
            "WINDOW_FRAME",
            "COUCH",
            "TABLE",
            "BED",
            "LAMP",
            "PLANT",
            "SCREEN",
            "STORAGE",
            "GLOBAL_MESH",
            "OTHER"
        };

        /// <summary>
        /// The Meta label for raw text: one of <see cref="SupportedMetaLabels"/>, OTHER for text the
        /// vocabulary does not know, for a class Meta has no label for (a chair, a cup) and for no text.
        /// </summary>
        public static string MapToMetaLabel(string rawLabel)
        {
            return TagVocabulary.Default.ToSemanticLabel(rawLabel);
        }

        /// <summary>
        /// The canonical tag for raw text ("sofa" and "a loveseat" are "couch"), empty when there is
        /// no usable text. A class without a Meta label still has a tag, which is what tracking uses.
        /// </summary>
        public static string NormalizeTag(string rawLabel)
        {
            return TagVocabulary.Default.Normalize(rawLabel);
        }
    }
}
