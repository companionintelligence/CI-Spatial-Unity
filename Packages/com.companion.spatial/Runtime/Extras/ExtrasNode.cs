using Newtonsoft.Json.Linq;

namespace Companion.Spatial.Extras
{
    /// <summary>
    /// The base of every part of the extras model that comes from a JSON object. It holds the keys the reader did not
    /// recognise, so a file written by a newer scanner, or edited by another tool, keeps them when the scanner reads it
    /// and writes it back (a re-export, a merge): the reader never drops a key, and the writer puts these back after
    /// the keys it knows.
    /// </summary>
    public abstract class ExtrasNode
    {
        /// <summary>
        /// The keys of this part of the JSON that the model has no property for, with their values as they were read.
        /// Never null. A file read from schema v1 keeps there, under the key <c>"v1"</c>, the v1 fields that v2 has no
        /// place for (a pin's <c>hit_type</c> and <c>prompt</c>, an anchor's volume), so nothing is lost by upgrading.
        /// </summary>
        public JObject Unknown { get; } = new JObject();
    }
}
