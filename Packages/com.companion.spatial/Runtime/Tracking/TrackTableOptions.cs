using System;
using Companion.Spatial.Vocabulary;

namespace Companion.Spatial.Tracking
{
    /// <summary>
    /// The settings of a <see cref="TrackTable"/>. The defaults are those of the scanner's inline tracker in
    /// <c>SpatialCapturePipeline</c> (<c>trackDistanceThreshold</c> 0.4 m, <c>smoothingAlpha</c> 0.5), except the lost
    /// time: the pipeline dropped a track after <c>trackMaxAgeSeconds</c> = 2.5 s, which made a manual capture ten
    /// seconds later pin the same object again; the table marks it lost after 10 s and keeps it. The values are checked
    /// once, here, and cannot change afterwards.
    /// </summary>
    public sealed class TrackTableOptions
    {
        /// <summary>Creates options; every argument is optional and falls back to its default.</summary>
        /// <param name="associationDistance">How far, in metres, a detection may be from a track's last observed position to be the same object. Must be above 0. Default 0.4.</param>
        /// <param name="smoothingAlpha">The weight, 0 to 1, of a new observation in the exponential moving average of a track's position (0 keeps the first position, 1 follows the latest). Default 0.5.</param>
        /// <param name="lostAfterSeconds">Scan seconds without an observation after which an active track is lost. Must be above 0. Default 10.</param>
        /// <param name="retireAfterSeconds">Scan seconds without an observation after which a track is removed for good; at least <paramref name="lostAfterSeconds"/>. Default: never.</param>
        /// <param name="maxObservations">How many recent observations (frame, box, score) a track keeps. At least 1. Default 16.</param>
        /// <param name="vocabulary">The tag vocabulary that decides which labels are the same kind of object; null means <see cref="TagVocabulary.Default"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">A value is outside its range, or is NaN.</exception>
        public TrackTableOptions(
            double associationDistance = DefaultAssociationDistance,
            double smoothingAlpha = DefaultSmoothingAlpha,
            double lostAfterSeconds = DefaultLostAfterSeconds,
            double retireAfterSeconds = double.PositiveInfinity,
            int maxObservations = DefaultMaxObservations,
            TagVocabulary vocabulary = null)
        {
            if (!(associationDistance > 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(associationDistance), associationDistance, "The association distance must be above 0.");
            }

            if (!(smoothingAlpha >= 0.0 && smoothingAlpha <= 1.0))
            {
                throw new ArgumentOutOfRangeException(nameof(smoothingAlpha), smoothingAlpha, "The smoothing alpha must be from 0 to 1.");
            }

            if (!(lostAfterSeconds > 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(lostAfterSeconds), lostAfterSeconds, "The lost time must be above 0.");
            }

            if (!(retireAfterSeconds >= lostAfterSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(retireAfterSeconds), retireAfterSeconds, "A track cannot be retired before it is lost: the retire time must be at least the lost time.");
            }

            if (maxObservations < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxObservations), maxObservations, "A track keeps at least one observation.");
            }

            AssociationDistance = associationDistance;
            SmoothingAlpha = smoothingAlpha;
            LostAfterSeconds = lostAfterSeconds;
            RetireAfterSeconds = retireAfterSeconds;
            MaxObservations = maxObservations;
            Vocabulary = vocabulary ?? TagVocabulary.Default;
        }

        /// <summary>The default association distance, 0.4 m (the pipeline's <c>trackDistanceThreshold</c>).</summary>
        public const double DefaultAssociationDistance = 0.4;

        /// <summary>The default smoothing alpha, 0.5 (the pipeline's <c>smoothingAlpha</c>).</summary>
        public const double DefaultSmoothingAlpha = 0.5;

        /// <summary>The default lost time, 10 s of scan time.</summary>
        public const double DefaultLostAfterSeconds = 10.0;

        /// <summary>The default length of a track's observation history, 16.</summary>
        public const int DefaultMaxObservations = 16;

        /// <summary>How far, in metres, a detection may be from a track's last observed position to join it (inclusive).</summary>
        public double AssociationDistance { get; }

        /// <summary>The weight of a new observation in a track's smoothed position, 0 to 1.</summary>
        public double SmoothingAlpha { get; }

        /// <summary>Scan seconds without an observation after which an active track is lost (reached at exactly this long).</summary>
        public double LostAfterSeconds { get; }

        /// <summary>Scan seconds without an observation after which a track is removed; <c>double.PositiveInfinity</c> means never.</summary>
        public double RetireAfterSeconds { get; }

        /// <summary>How many recent observations a track keeps.</summary>
        public int MaxObservations { get; }

        /// <summary>The tag vocabulary that decides which detections are the same kind of object.</summary>
        public TagVocabulary Vocabulary { get; }
    }
}
