using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Companion.Spatial.Extras
{
    /// <summary>
    /// What <see cref="ExtrasReader"/> made of a document: the model, or a clear error, and a list of warnings for what
    /// was odd but could be read. Reading never throws on bad input; check <see cref="Success"/>.
    /// </summary>
    public sealed class ExtrasReadResult
    {
        private ExtrasReadResult(ScanExtras extras, int sourceVersion, string error, IReadOnlyList<string> warnings)
        {
            Extras = extras;
            SourceVersion = sourceVersion;
            Error = error;
            Warnings = warnings;
        }

        /// <summary>True when the document could be read; then <see cref="Extras"/> is set and <see cref="Error"/> is null.</summary>
        public bool Success
        {
            get { return Error == null; }
        }

        /// <summary>The document as a v2 model (a v1 file is upgraded as it is read); null when <see cref="Success"/> is false.</summary>
        public ScanExtras Extras { get; }

        /// <summary>
        /// The schema version the document declared, or was taken to be: 1 for a v1 file (including one with no
        /// <c>schema_version</c>), 2 for v2, a higher number for a newer file (read as v2). 0 when the read failed.
        /// </summary>
        public int SourceVersion { get; }

        /// <summary>Why the document could not be read, in one sentence; null when it could.</summary>
        public string Error { get; }

        /// <summary>
        /// Things the reader noticed and worked around: a value of the wrong type, an entry that was skipped, an id it
        /// had to make up. Never null; empty for a clean document.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; }

        internal static ExtrasReadResult Ok(ScanExtras extras, int sourceVersion, IReadOnlyList<string> warnings)
        {
            return new ExtrasReadResult(extras, sourceVersion, null, new ReadOnlyCollection<string>(new List<string>(warnings)));
        }

        internal static ExtrasReadResult Fail(string error)
        {
            return new ExtrasReadResult(null, 0, error ?? "The document could not be read.", Array.Empty<string>());
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Success ? "Read v" + SourceVersion + " with " + Warnings.Count + " warning(s)" : "Read failed: " + Error;
        }
    }
}
