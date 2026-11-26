using System;
using System.Globalization;

namespace Companion.Spatial
{
    /// <summary>
    /// A point or a direction in the scanner's world space, in metres, as three <c>double</c>s. It stands in for
    /// <c>UnityEngine.Vector3</c> so the tracking and export code needs no engine: the scanner converts a detection's
    /// raycast hit into a <see cref="Vec3"/> and a pin's position back from one. The axes are those of the scene the
    /// scanner exports (Unity world: +Y up); this type does not change them.
    /// </summary>
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        /// <summary>Creates a vector from its three components, in metres for a position.</summary>
        /// <param name="x">Component along the world X axis.</param>
        /// <param name="y">Component along the world Y axis (up).</param>
        /// <param name="z">Component along the world Z axis.</param>
        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>The component along the world X axis.</summary>
        public double X { get; }

        /// <summary>The component along the world Y axis (up).</summary>
        public double Y { get; }

        /// <summary>The component along the world Z axis.</summary>
        public double Z { get; }

        /// <summary>The origin: (0, 0, 0).</summary>
        public static Vec3 Zero
        {
            get { return new Vec3(0.0, 0.0, 0.0); }
        }

        /// <summary>The length of the vector (its distance from the origin, for a position), in metres.</summary>
        public double Length
        {
            get { return Math.Sqrt(X * X + Y * Y + Z * Z); }
        }

        /// <summary>True when all three components are real numbers (none is NaN or infinite).</summary>
        public bool IsFinite
        {
            get { return double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z); }
        }

        /// <summary>
        /// The vector scaled to length 1, or <see cref="Zero"/> when it has no usable direction (it is zero, or has a
        /// NaN or infinite component). A surface normal that was never measured stays a zero vector this way.
        /// </summary>
        public Vec3 Normalized()
        {
            double length = Length;
            if (!(length > 0.0) || double.IsInfinity(length))
            {
                return Zero;
            }

            return new Vec3(X / length, Y / length, Z / length);
        }

        /// <summary>Adds two vectors component by component.</summary>
        public static Vec3 operator +(Vec3 a, Vec3 b)
        {
            return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        /// <summary>Subtracts <paramref name="b"/> from <paramref name="a"/> component by component.</summary>
        public static Vec3 operator -(Vec3 a, Vec3 b)
        {
            return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        /// <summary>The vector pointing the other way.</summary>
        public static Vec3 operator -(Vec3 a)
        {
            return new Vec3(-a.X, -a.Y, -a.Z);
        }

        /// <summary>Scales a vector by a number.</summary>
        public static Vec3 operator *(Vec3 a, double s)
        {
            return new Vec3(a.X * s, a.Y * s, a.Z * s);
        }

        /// <summary>Scales a vector by a number.</summary>
        public static Vec3 operator *(double s, Vec3 a)
        {
            return new Vec3(a.X * s, a.Y * s, a.Z * s);
        }

        /// <summary>True when every component is equal (exactly, as for <see cref="Equals(Vec3)"/>).</summary>
        public static bool operator ==(Vec3 a, Vec3 b)
        {
            return a.Equals(b);
        }

        /// <summary>True when some component differs.</summary>
        public static bool operator !=(Vec3 a, Vec3 b)
        {
            return !a.Equals(b);
        }

        /// <summary>The dot product: the sum of the products of the components.</summary>
        public static double Dot(Vec3 a, Vec3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        /// <summary>
        /// The straight-line distance between two positions, in metres. The track table compares this with its
        /// association distance (the scanner's <c>trackDistanceThreshold</c>, 0.4 m).
        /// </summary>
        public static double Distance(Vec3 a, Vec3 b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// The point <paramref name="t"/> of the way from <paramref name="a"/> to <paramref name="b"/>, like
        /// <c>UnityEngine.Vector3.Lerp</c>: <paramref name="t"/> is clamped to 0..1 (a NaN counts as 0), so the result
        /// never overshoots. With <paramref name="t"/> as the smoothing alpha this is the scanner's temporal smoothing.
        /// </summary>
        public static Vec3 Lerp(Vec3 a, Vec3 b, double t)
        {
            if (!(t > 0.0))
            {
                return a;
            }

            if (t >= 1.0)
            {
                return b;
            }

            return new Vec3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        }

        /// <summary>The component-wise smaller of two vectors: the low corner of the box around both points.</summary>
        public static Vec3 Min(Vec3 a, Vec3 b)
        {
            return new Vec3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
        }

        /// <summary>The component-wise larger of two vectors: the high corner of the box around both points.</summary>
        public static Vec3 Max(Vec3 a, Vec3 b)
        {
            return new Vec3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
        }

        /// <summary>True when every component is equal. Two NaN components count as equal, so a value equals itself.</summary>
        public bool Equals(Vec3 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Vec3 other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Hash(X);
                hash = hash * 31 + Hash(Y);
                hash = hash * 31 + Hash(Z);
                return hash;
            }
        }

        /// <summary>The vector as "(x, y, z)" with a period as the decimal separator, whatever the current culture.</summary>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0}, {1}, {2})", X.ToString("R", CultureInfo.InvariantCulture), Y.ToString("R", CultureInfo.InvariantCulture), Z.ToString("R", CultureInfo.InvariantCulture));
        }

        // -0.0 equals 0.0, so both must hash alike.
        private static int Hash(double value)
        {
            return (value == 0.0 ? 0.0 : value).GetHashCode();
        }
    }
}
