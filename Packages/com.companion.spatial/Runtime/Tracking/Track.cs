using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Companion.Spatial.Vocabulary;

namespace Companion.Spatial.Tracking
{
    /// <summary>
    /// One physical object as the scanner has followed it over several captures: the fusion of every detection that
    /// was matched to it. The table owns and changes tracks; the scanner only reads them (to place and label a pin) and
    /// refers to one by <see cref="Id"/>.
    /// </summary>
    public sealed class Track
    {
        /// <summary>The highest confidence a track reports, however many detections agree (a detector is never certain).</summary>
        public const double MaxConfidence = 0.99;

        private const int MaxVoteEntries = 32;

        private readonly TagVocabulary _vocabulary;
        private readonly List<TrackObservation> _observations = new List<TrackObservation>();
        private readonly ScoreVote _tagVote = new ScoreVote(StringComparer.Ordinal, MaxVoteEntries);
        private readonly ScoreVote _textVote = new ScoreVote(StringComparer.OrdinalIgnoreCase, MaxVoteEntries);
        private double _survival = 1.0;

        internal Track(int id, string key, TagVocabulary vocabulary)
        {
            Id = id;
            Key = key;
            _vocabulary = vocabulary;
            Observations = new ReadOnlyCollection<TrackObservation>(_observations);
            Tag = key;
            Text = string.Empty;
        }

        /// <summary>The track's number: 1, 2, 3 in the order tracks are found, never reused, even after a track is retired.</summary>
        public int Id { get; }

        /// <summary>
        /// The canonical tag of the object (see <see cref="TagVocabulary"/>): the tag with the highest summed score over
        /// the track's observations. Detections only join a track of their own tag, so today it is the tag the track
        /// was found with; the vote keeps the rule honest if matching is ever widened.
        /// </summary>
        public string Tag { get; private set; }

        /// <summary>
        /// The label as the detector wrote it ("TV screen", "Sofa"), the spelling with the highest summed score over all
        /// the observations (spellings that differ only in case, punctuation or a leading article count as one), shown
        /// as it was first written. It is what a pin displays; <see cref="Tag"/> is what tracking and export compare.
        /// </summary>
        public string Text { get; private set; }

        /// <summary>The Meta label of <see cref="Tag"/>: one of <see cref="MetaLabels.All"/>, OTHER when Meta has none.</summary>
        public string SemanticLabel
        {
            get { return _vocabulary.ToSemanticLabel(Tag); }
        }

        /// <summary>
        /// The fused position in world metres: the first observation, then an exponential moving average of the
        /// observations (the smoothing alpha is the weight of the new one). It is what a pin is placed at.
        /// </summary>
        public Vec3 Position { get; private set; }

        /// <summary>The raw position of the latest observation. Matching compares new detections with this, as the scanner's tracker always did.</summary>
        public Vec3 LastObservedPosition { get; private set; }

        /// <summary>
        /// The smoothed surface normal, a unit vector, or <see cref="Vec3.Zero"/> while no detection has brought one.
        /// </summary>
        public Vec3 Normal { get; private set; }

        /// <summary>
        /// How sure the scanner is that the object is there: 1 - (1 - s1)(1 - s2)... over the scores of every
        /// observation (noisy-OR), so each agreeing detection raises it, never above <see cref="MaxConfidence"/>.
        /// </summary>
        public double Confidence { get; private set; }

        /// <summary>Whether the object has been seen within the table's lost time.</summary>
        public TrackState State { get; internal set; }

        /// <summary>The scan time of the first observation, in seconds.</summary>
        public double FirstSeen { get; private set; }

        /// <summary>The scan time of the latest observation, in seconds.</summary>
        public double LastSeen { get; private set; }

        /// <summary>How many detections have been matched to the track since it was found (not only the ones still in <see cref="Observations"/>).</summary>
        public int ObservationCount { get; private set; }

        /// <summary>The most recent observations, oldest first, at most the table's history length (default 16).</summary>
        public IReadOnlyList<TrackObservation> Observations { get; }

        /// <summary>The tag detections are matched on: the tag the track was found with.</summary>
        internal string Key { get; }

        internal void Observe(double timestamp, string tag, string text, double score, Detection3D detection, double smoothingAlpha, int maxObservations)
        {
            ObservationCount++;
            if (ObservationCount == 1)
            {
                FirstSeen = timestamp;
                Position = detection.Position;
            }
            else
            {
                Position = Vec3.Lerp(Position, detection.Position, smoothingAlpha);
            }

            LastObservedPosition = detection.Position;
            LastSeen = timestamp;
            BlendNormal(detection.Normal, smoothingAlpha);

            _survival *= 1.0 - score;
            Confidence = Math.Min(MaxConfidence, 1.0 - _survival);

            _tagVote.Add(tag, tag, score);
            _textVote.Add(TagText.Clean(text), text, score);
            Tag = _tagVote.Winner;
            Text = _textVote.Winner;

            _observations.Add(new TrackObservation(detection.FrameId ?? string.Empty, detection.Box, score));
            if (_observations.Count > maxObservations)
            {
                _observations.RemoveRange(0, _observations.Count - maxObservations);
            }
        }

        // A detection without a usable normal leaves it alone; the first usable one sets it; later ones are blended.
        private void BlendNormal(Vec3 detected, double alpha)
        {
            Vec3 unit = detected.IsFinite ? detected.Normalized() : Vec3.Zero;
            if (unit == Vec3.Zero)
            {
                return;
            }

            if (Normal == Vec3.Zero)
            {
                Normal = unit;
                return;
            }

            Vec3 blended = Vec3.Lerp(Normal, unit, alpha).Normalized();
            Normal = blended == Vec3.Zero ? unit : blended;
        }
    }
}
