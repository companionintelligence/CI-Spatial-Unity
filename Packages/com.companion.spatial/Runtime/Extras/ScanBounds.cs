namespace Companion.Spatial.Extras
{
    /// <summary>
    /// An axis-aligned box in the space's coordinate frame, as a centre and a size, in metres: the same shape as the v1
    /// extras' <c>bounds</c> (<c>center</c> and <c>size</c>), which the scanner computed with Unity's <c>Bounds</c>. It is the
    /// extent of a whole scan (<see cref="ScanExtras.Bounds"/>) or of one object (<see cref="ScanObject.Bounds"/>).
    /// </summary>
    public sealed class ScanBounds : ExtrasNode
    {
        /// <summary>Creates an empty box at the origin.</summary>
        public ScanBounds()
        {
        }

        /// <summary>Creates a box from its centre and size.</summary>
        /// <param name="center">The centre, in metres.</param>
        /// <param name="size">The size along each axis, in metres (not negative).</param>
        public ScanBounds(Vec3 center, Vec3 size)
        {
            Center = center;
            Size = size;
        }

        /// <summary>The centre of the box, in metres.</summary>
        public Vec3 Center { get; set; }

        /// <summary>The size of the box along each axis, in metres.</summary>
        public Vec3 Size { get; set; }

        /// <summary>The corner with the smallest coordinates.</summary>
        public Vec3 Min
        {
            get { return Center - Size * 0.5; }
        }

        /// <summary>The corner with the largest coordinates.</summary>
        public Vec3 Max
        {
            get { return Center + Size * 0.5; }
        }

        /// <summary>Makes the box with the given corners.</summary>
        /// <param name="min">The corner with the smallest coordinates.</param>
        /// <param name="max">The corner with the largest coordinates.</param>
        public static ScanBounds FromMinMax(Vec3 min, Vec3 max)
        {
            return new ScanBounds((min + max) * 0.5, max - min);
        }
    }
}
