using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Companion.Spatial.Vocabulary;

namespace Companion.Spatial.Tracking
{
    /// <summary>
    /// Fuses detections that already have a world position into tracks, one track per physical object, across
    /// captures. It generalises the inline tracker of <c>SpatialCapturePipeline</c> (<c>AssignTrackId</c>,
    /// <c>ApplySmoothing</c>) and is engine-free: the scanner raycasts each box to a position, calls
    /// <see cref="Update"/> once per capture, and turns the returned events into pins.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Matching. A detection may join a track of the same canonical tag (see <see cref="TagVocabulary"/>) whose last
    /// observed position is within <see cref="TrackTableOptions.AssociationDistance"/> (inclusive). Within one capture
    /// the pairs are taken nearest first across all detections and tracks, ties going to the lower detection index and
    /// then the lower track id, so the result never depends on the order the detections are listed in and the same
    /// input always gives the same output. Each detection joins at most one track and each track takes at most one
    /// detection per capture: two detections of one capture are never the same object. A detection that matches nothing
    /// starts a new track. Active and lost tracks are both candidates.
    /// </para>
    /// <para>
    /// Fusion. A track's position is the first observation, then an exponential moving average; its confidence is the
    /// noisy-OR of the scores, capped at <see cref="Track.MaxConfidence"/>; its tag and text are chosen by summed
    /// score; it keeps the most recent observations.
    /// </para>
    /// <para>
    /// Lifecycle. A track not observed for <see cref="TrackTableOptions.LostAfterSeconds"/> of scan time becomes lost,
    /// reported once, by the next <see cref="Update"/> or by <see cref="Tick"/>. A lost track that is detected again is
    /// revived with the same id; a revival is always preceded by its Lost event, even when the same call reports both
    /// (a capture ten seconds after the last sighting). Lost tracks stay in the table until
    /// <see cref="TrackTableOptions.RetireAfterSeconds"/> (never, by default).
    /// </para>
    /// <para>
    /// Bad input is ignored, not thrown on: a detection with a NaN score, a position that is not finite, or a label
    /// that normalizes to nothing is skipped and counted in <see cref="RejectedDetections"/>; a score outside 0..1 is
    /// clamped; a timestamp that is NaN or earlier than the latest one is taken as the latest one. The table keeps no
    /// static state, uses no clock and no random numbers, and is not thread safe.
    /// </para>
    /// </remarks>
    public sealed class TrackTable
    {
        private readonly TrackTableOptions _options;
        private readonly List<Track> _tracks = new List<Track>();
        private readonly ReadOnlyCollection<Track> _view;
        private readonly Dictionary<string, List<Track>> _byKey = new Dictionary<string, List<Track>>(StringComparer.Ordinal);
        private int _nextId = 1;
        private bool _hasClock;

        /// <summary>Creates a table with the default options.</summary>
        public TrackTable()
            : this(new TrackTableOptions())
        {
        }

        /// <summary>Creates a table with the given options.</summary>
        /// <param name="options">The settings; they cannot change afterwards.</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        public TrackTable(TrackTableOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _view = new ReadOnlyCollection<Track>(_tracks);
        }

        /// <summary>The settings of the table.</summary>
        public TrackTableOptions Options
        {
            get { return _options; }
        }

        /// <summary>Every track in the table, active and lost, in id order (a live read-only view: it changes as the table does).</summary>
        public IReadOnlyList<Track> Tracks
        {
            get { return _view; }
        }

        /// <summary>The latest scan time the table has accepted, in seconds; 0 until the first <see cref="Update"/> or <see cref="Tick"/>.</summary>
        public double Clock { get; private set; }

        /// <summary>How many detections the table has ignored so far because they could not be tracked (a NaN score, a position that is not finite, or a label with no letter in it).</summary>
        public int RejectedDetections { get; private set; }

        /// <summary>Finds a track by id.</summary>
        /// <param name="id">The id of a track (see <see cref="Track.Id"/>).</param>
        /// <param name="track">The track, or null when there is none (an id that was never used, or a retired track).</param>
        /// <returns>True when the track is in the table.</returns>
        public bool TryGetTrack(int id, out Track track)
        {
            // ids go up as tracks are added and tracks only leave, so the list is sorted by id
            int low = 0;
            int high = _tracks.Count - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                int found = _tracks[middle].Id;
                if (found == id)
                {
                    track = _tracks[middle];
                    return true;
                }

                if (found < id)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            track = null;
            return false;
        }

        /// <summary>
        /// Takes one capture: reports the tracks that went lost or were retired by the passage of time, then matches
        /// the detections to tracks and reports what happened to each.
        /// </summary>
        /// <param name="timestamp">The capture's scan time in seconds. Time never goes backwards: NaN or a value earlier than the latest is taken as the latest.</param>
        /// <param name="detections">The capture's detections, in the order the scanner lists them; null or empty is fine.</param>
        /// <returns>
        /// A new list the caller may keep: first Lost and Retired events in track id order, then one Found, Updated or
        /// Revived event per usable detection in the order of <paramref name="detections"/>.
        /// </returns>
        public IReadOnlyList<TrackEvent> Update(double timestamp, IReadOnlyList<Detection3D> detections)
        {
            double now = AcceptTime(timestamp);
            var events = new List<TrackEvent>();
            Sweep(now, events);
            if (detections != null && detections.Count > 0)
            {
                Associate(now, detections, events);
            }

            return events;
        }

        /// <summary>
        /// Moves the clock on without a capture: reports the tracks that went lost or were retired by the passage of
        /// time. <see cref="Update"/> does this first, so calling it is only needed between captures.
        /// </summary>
        /// <param name="timestamp">The scan time in seconds (see <see cref="Update"/> for how odd values are taken).</param>
        /// <returns>The Lost and Retired events, in track id order.</returns>
        public IReadOnlyList<TrackEvent> Tick(double timestamp)
        {
            double now = AcceptTime(timestamp);
            var events = new List<TrackEvent>();
            Sweep(now, events);
            return events;
        }

        private double AcceptTime(double timestamp)
        {
            double now = timestamp;
            if (double.IsNaN(now) || double.IsInfinity(now) || (_hasClock && now < Clock))
            {
                now = Clock;
            }

            _hasClock = true;
            Clock = now;
            return now;
        }

        private void Sweep(double now, List<TrackEvent> events)
        {
            List<Track> retired = null;
            foreach (Track track in _tracks)
            {
                double silent = now - track.LastSeen;
                if (track.State == TrackState.Active && silent >= _options.LostAfterSeconds)
                {
                    track.State = TrackState.Lost;
                    events.Add(new TrackEvent(TrackEventKind.Lost, track.Id, now, -1));
                }

                if (silent >= _options.RetireAfterSeconds)
                {
                    if (retired == null)
                    {
                        retired = new List<Track>();
                    }

                    retired.Add(track);
                    events.Add(new TrackEvent(TrackEventKind.Retired, track.Id, now, -1));
                }
            }

            if (retired == null)
            {
                return;
            }

            foreach (Track track in retired)
            {
                _tracks.Remove(track);
                List<Track> bucket = _byKey[track.Key];
                bucket.Remove(track);
                if (bucket.Count == 0)
                {
                    _byKey.Remove(track.Key);
                }
            }
        }

        private void Associate(double now, IReadOnlyList<Detection3D> detections, List<TrackEvent> events)
        {
            var usable = new List<Candidate>(detections.Count);
            for (int i = 0; i < detections.Count; i++)
            {
                Candidate candidate;
                if (TryAccept(i, detections[i], out candidate))
                {
                    usable.Add(candidate);
                }
                else
                {
                    RejectedDetections++;
                }
            }

            // every (detection, track) pair within reach, nearest first
            var pairs = new List<Pair>();
            foreach (Candidate candidate in usable)
            {
                List<Track> bucket;
                if (!_byKey.TryGetValue(candidate.Tag, out bucket))
                {
                    continue;
                }

                foreach (Track track in bucket)
                {
                    double distance = Vec3.Distance(track.LastObservedPosition, candidate.Detection.Position);
                    if (distance <= _options.AssociationDistance)
                    {
                        pairs.Add(new Pair(distance, candidate.Index, track));
                    }
                }
            }

            pairs.Sort(ComparePairs);

            var joined = new Dictionary<int, Track>();
            var taken = new HashSet<int>();
            foreach (Pair pair in pairs)
            {
                if (joined.ContainsKey(pair.Index) || taken.Contains(pair.Track.Id))
                {
                    continue;
                }

                joined[pair.Index] = pair.Track;
                taken.Add(pair.Track.Id);
            }

            foreach (Candidate candidate in usable)
            {
                Track track;
                TrackEventKind kind;
                if (joined.TryGetValue(candidate.Index, out track))
                {
                    kind = track.State == TrackState.Lost ? TrackEventKind.Revived : TrackEventKind.Updated;
                    track.State = TrackState.Active;
                }
                else
                {
                    track = AddTrack(candidate.Tag);
                    kind = TrackEventKind.Found;
                }

                track.Observe(now, candidate.Tag, candidate.Text, candidate.Score, candidate.Detection, _options.SmoothingAlpha, _options.MaxObservations);
                events.Add(new TrackEvent(kind, track.Id, now, candidate.Index));
            }
        }

        private bool TryAccept(int index, Detection3D detection, out Candidate candidate)
        {
            candidate = default;
            if (!detection.Position.IsFinite || double.IsNaN(detection.Score))
            {
                return false;
            }

            string tag = _options.Vocabulary.Normalize(detection.Label);
            if (tag.Length == 0)
            {
                return false;
            }

            double score = detection.Score < 0.0 ? 0.0 : (detection.Score > 1.0 ? 1.0 : detection.Score);
            candidate = new Candidate(index, detection, tag, TagText.Collapse(detection.Label), score);
            return true;
        }

        private Track AddTrack(string key)
        {
            var track = new Track(_nextId++, key, _options.Vocabulary);
            _tracks.Add(track);
            List<Track> bucket;
            if (!_byKey.TryGetValue(key, out bucket))
            {
                bucket = new List<Track>();
                _byKey[key] = bucket;
            }

            bucket.Add(track);
            return track;
        }

        private static int ComparePairs(Pair a, Pair b)
        {
            int byDistance = a.Distance.CompareTo(b.Distance);
            if (byDistance != 0)
            {
                return byDistance;
            }

            int byDetection = a.Index.CompareTo(b.Index);
            return byDetection != 0 ? byDetection : a.Track.Id.CompareTo(b.Track.Id);
        }

        private readonly struct Candidate
        {
            internal Candidate(int index, Detection3D detection, string tag, string text, double score)
            {
                Index = index;
                Detection = detection;
                Tag = tag;
                Text = text;
                Score = score;
            }

            internal int Index { get; }

            internal Detection3D Detection { get; }

            internal string Tag { get; }

            internal string Text { get; }

            internal double Score { get; }
        }

        private readonly struct Pair
        {
            internal Pair(double distance, int index, Track track)
            {
                Distance = distance;
                Index = index;
                Track = track;
            }

            internal double Distance { get; }

            internal int Index { get; }

            internal Track Track { get; }
        }
    }
}
