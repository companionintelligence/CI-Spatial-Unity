namespace Companion.Spatial.Extras
{
    /// <summary>
    /// A position and an orientation in the space's coordinate frame (<see cref="ScanSpace.Frame"/>): where an object
    /// stands, where a camera was, where a Space Setup plane lies. The web Scan contract's <c>Pose</c> has the same
    /// two members, <c>position</c> and <c>quaternion</c>.
    /// </summary>
    public sealed class ScanPose : ExtrasNode
    {
        /// <summary>Creates a pose at the origin with no rotation.</summary>
        public ScanPose()
        {
            Rotation = Quat.Identity;
        }

        /// <summary>Creates a pose.</summary>
        /// <param name="position">The position in metres.</param>
        /// <param name="rotation">The orientation.</param>
        public ScanPose(Vec3 position, Quat rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        /// <summary>The position, in metres (JSON <c>position</c>, an object with x, y and z).</summary>
        public Vec3 Position { get; set; }

        /// <summary>The orientation (JSON <c>quaternion</c>, an object with x, y, z and w).</summary>
        public Quat Rotation { get; set; }
    }
}
