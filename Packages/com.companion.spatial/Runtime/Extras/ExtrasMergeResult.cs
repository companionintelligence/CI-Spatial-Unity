using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Companion.Spatial.Extras
{
    /// <summary>The outcome of <see cref="ExtrasMerge"/>: the merged document and what the merge had to say.</summary>
    public sealed class ExtrasMergeResult
    {
        internal ExtrasMergeResult(ScanExtras merged, IReadOnlyList<string> warnings, bool existingDiscarded)
        {
            Merged = merged;
            Warnings = new ReadOnlyCollection<string>(new List<string>(warnings));
            ExistingDiscarded = existingDiscarded;
        }

        /// <summary>
        /// The merged document, a new <see cref="ScanExtras"/>. It shares the objects, frames and planes of the two
        /// documents it was made from, so treat those two as consumed.
        /// </summary>
        public ScanExtras Merged { get; }

        /// <summary>
        /// Things the merge worked around, each worth a log line: an earlier document that could not be read, an
        /// object with no id, consent that was not carried over. Never null.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>
        /// True when an earlier document was given but could not be used (it is corrupt, or is for another space or in
        /// another coordinate frame), so the merged document is the new export alone.
        /// </summary>
        public bool ExistingDiscarded { get; }
    }
}
