using System.Collections.Generic;
using System.Globalization;
using Companion.Spatial.Vocabulary;

namespace Companion.Spatial.Extras
{
    /// <summary>What one read of a document carries around: the vocabulary to derive labels with and the warnings so far.</summary>
    internal sealed class ReadContext
    {
        internal ReadContext(TagVocabulary vocabulary)
        {
            Vocabulary = vocabulary ?? TagVocabulary.Default;
        }

        internal TagVocabulary Vocabulary { get; }

        internal List<string> Warnings { get; } = new List<string>();

        internal void Warn(string message)
        {
            Warnings.Add(message);
        }

        /// <summary>Adds an item to a list by id: an id seen before replaces the earlier item in its place, with a warning.</summary>
        internal void AddById<T>(List<T> list, Dictionary<string, int> positions, T item, string id, string listName)
        {
            int position;
            if (positions.TryGetValue(id, out position))
            {
                list[position] = item;
                Warn(listName + ": the id \"" + id + "\" is listed more than once; the last entry is kept");
                return;
            }

            positions[id] = list.Count;
            list.Add(item);
        }

        internal static string At(string list, int index)
        {
            return list + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
        }
    }
}
