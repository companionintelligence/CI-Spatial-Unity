using System;
using System.Collections.Generic;
using System.Globalization;
using Companion.Spatial.Tracking;

namespace Companion.Spatial.Extras
{
    /// <summary>
    /// One thing in the room: a pin in v1 terms. It is what the scanner found with the camera and the track table, or
    /// what the headset's scene model reported, or what the user placed. Objects are identified by <see cref="Id"/>,
    /// which is what a re-export merges on.
    /// </summary>
    public sealed class ScanObject : ExtrasNode
    {
        /// <summary>Creates an empty object: no id, track 0, a CV object at the origin.</summary>
        public ScanObject()
        {
            Id = string.Empty;
            Label = new ScanLabel();
            Pose = new ScanPose();
            Observations = new List<ScanObservation>();
        }

        /// <summary>
        /// The object's id (JSON <c>id</c>): unique in the file and stable across exports, so a re-export replaces the
        /// object instead of adding it a second time. The scanner's track ids start at 1 in every app run, so an id
        /// must also be unique across runs: see <see cref="MakeId"/>.
        /// </summary>
        public string Id { get; set; }

        /// <summary>The number of the object's <see cref="Companion.Spatial.Tracking.Track"/> in the app run that found it; 0 when it was not tracked, such as a headset anchor (JSON <c>track_id</c>).</summary>
        public int TrackId { get; set; }

        /// <summary>What the object is called (JSON <c>label</c>).</summary>
        public ScanLabel Label { get; set; }

        /// <summary>How sure the scanner is that the object is there, 0 to 1 (JSON <c>confidence</c>): the fused confidence of a track, 1 for what the headset reports.</summary>
        public double Confidence { get; set; }

        /// <summary>Who found the object (JSON <c>source</c>).</summary>
        public ObjectSource Source { get; set; }

        /// <summary>Where the object is: the fused position of its track, and its rotation (JSON <c>pose</c>).</summary>
        public ScanPose Pose { get; set; }

        /// <summary>The surface normal where the object was placed, a unit vector, or null when unknown (JSON <c>normal</c>).</summary>
        public Vec3? Normal { get; set; }

        /// <summary>The box around the object in world space, or null when its extent is unknown; a CV pin has none, a headset volume has one (JSON <c>bounds</c>).</summary>
        public ScanBounds Bounds { get; set; }

        /// <summary>The headset's anchor id for an object from the scene model; null or empty for the rest (JSON <c>anchor_uuid</c>).</summary>
        public string AnchorUuid { get; set; }

        /// <summary>The object's most recent sightings, oldest first; never null (JSON <c>observations</c>).</summary>
        public List<ScanObservation> Observations { get; }

        /// <summary>
        /// An object id for a track that is unique across app runs: the capture session id and the track id joined, such
        /// as "3f9a...-track-7", or "track-7" when there is no session id.
        /// </summary>
        /// <param name="sessionId">The id of the app run (the scanner's <c>captureSessionId</c>); null or empty for none.</param>
        /// <param name="trackId">The track's id.</param>
        public static string MakeId(string sessionId, int trackId)
        {
            string track = "track-" + trackId.ToString(CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(sessionId) ? track : sessionId + "-" + track;
        }

        /// <summary>
        /// Makes the object a track stands for: its id from the session and the track number, its label (the text of the
        /// most convincing spelling, the tag and the Meta label), the fused confidence and position, the smoothed normal
        /// (none while the track has none) and the track's observations. A track knows no rotation, so the pose has none.
        /// The source is <see cref="ObjectSource.Cv"/>.
        /// </summary>
        /// <param name="track">The track, from <see cref="Companion.Spatial.Tracking.TrackTable.Tracks"/>.</param>
        /// <param name="sessionId">The id of the app run (the scanner's <c>captureSessionId</c>), so the id stays unique in a file that several runs export to.</param>
        /// <exception cref="ArgumentNullException"><paramref name="track"/> is null.</exception>
        public static ScanObject FromTrack(Track track, string sessionId)
        {
            if (track == null)
            {
                throw new ArgumentNullException(nameof(track));
            }

            var result = new ScanObject
            {
                Id = MakeId(sessionId, track.Id),
                TrackId = track.Id,
                Label = new ScanLabel(track.Text, track.Tag, track.SemanticLabel),
                Confidence = track.Confidence,
                Source = ObjectSource.Cv,
                Pose = new ScanPose(track.Position, Quat.Identity)
            };

            if (track.Normal != Vec3.Zero)
            {
                result.Normal = track.Normal;
            }

            foreach (TrackObservation observation in track.Observations)
            {
                result.Observations.Add(new ScanObservation { FrameId = observation.FrameId, Box = observation.Box, Score = observation.Score });
            }

            return result;
        }

        /// <summary>
        /// An object id for something the headset's scene model reported, from its anchor uuid: "anchor-" and the
        /// uuid. The same anchor always gives the same id, so a re-export replaces its object (the scanner's pin
        /// manager replaces a pin by anchor uuid the same way).
        /// </summary>
        /// <param name="anchorUuid">The anchor's uuid.</param>
        public static string MakeAnchorId(string anchorUuid)
        {
            return "anchor-" + anchorUuid;
        }
    }
}
