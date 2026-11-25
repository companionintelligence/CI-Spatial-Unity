using System;
using System.Collections.Generic;

namespace Companion.Spatial.Vocabulary
{
    /// <summary>
    /// One row of the vocabulary: a canonical tag, the Meta label it stands for, and the other phrases that mean the
    /// same thing. A row is plain data; <see cref="TagVocabulary"/> checks it when the table is built.
    /// </summary>
    public sealed class TagEntry
    {
        /// <summary>Creates a row.</summary>
        /// <param name="tag">The canonical tag: lower case, singular, words separated by single spaces ("wall art").</param>
        /// <param name="semantic">One of the 16 <see cref="MetaLabels"/>; <see cref="MetaLabels.Other"/> when Meta has no label.</param>
        /// <param name="synonyms">Other phrases for the same thing, written singular; plurals, case, articles and punctuation are handled for them.</param>
        public TagEntry(string tag, string semantic, params string[] synonyms)
        {
            Tag = tag;
            Semantic = semantic;
            Synonyms = synonyms == null ? Array.Empty<string>() : Array.AsReadOnly((string[])synonyms.Clone());
        }

        /// <summary>The canonical tag.</summary>
        public string Tag { get; }

        /// <summary>The Meta label of the tag, one of <see cref="MetaLabels.All"/>.</summary>
        public string Semantic { get; }

        /// <summary>The other phrases that normalize to <see cref="Tag"/>.</summary>
        public IReadOnlyList<string> Synonyms { get; }
    }
}
