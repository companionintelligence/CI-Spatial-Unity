using System;
using System.Globalization;

namespace Companion.Spatial
{
    /// <summary>
    /// An orientation as a quaternion (x, y, z, w), in the same convention as <c>UnityEngine.Quaternion</c>. The
    /// extras carry a pin's or an anchor's rotation and a snapshot camera's rotation with it. This type only holds the
    /// four numbers; it does no rotation maths.
    /// </summary>
    public readonly struct Quat : IEquatable<Quat>
    {
        /// <summary>Creates a quaternion from its four components.</summary>
        /// <param name="x">The x component.</param>
        /// <param name="y">The y component.</param>
        /// <param name="z">The z component.</param>
        /// <param name="w">The w (scalar) component.</param>
        public Quat(double x, double y, double z, double w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        /// <summary>The x component.</summary>
        public double X { get; }

        /// <summary>The y component.</summary>
        public double Y { get; }

        /// <summary>The z component.</summary>
        public double Z { get; }

        /// <summary>The w (scalar) component.</summary>
        public double W { get; }

        /// <summary>The "no rotation" quaternion, (0, 0, 0, 1).</summary>
        public static Quat Identity
        {
            get { return new Quat(0.0, 0.0, 0.0, 1.0); }
        }

        /// <summary>True when all four components are real numbers (none is NaN or infinite).</summary>
        public bool IsFinite
        {
            get { return double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z) && double.IsFinite(W); }
        }

        /// <summary>True when every component is equal. Two NaN components count as equal.</summary>
        public bool Equals(Quat other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Quat other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Hash(X);
                hash = hash * 31 + Hash(Y);
                hash = hash * 31 + Hash(Z);
                hash = hash * 31 + Hash(W);
                return hash;
            }
        }

        /// <summary>The quaternion as "(x, y, z, w)" with a period as the decimal separator, whatever the current culture.</summary>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0}, {1}, {2}, {3})", X.ToString("R", CultureInfo.InvariantCulture), Y.ToString("R", CultureInfo.InvariantCulture), Z.ToString("R", CultureInfo.InvariantCulture), W.ToString("R", CultureInfo.InvariantCulture));
        }

        private static int Hash(double value)
        {
            return (value == 0.0 ? 0.0 : value).GetHashCode();
        }
    }
}
