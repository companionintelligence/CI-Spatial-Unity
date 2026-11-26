using System;
using System.Globalization;

namespace Companion.Spatial
{
    /// <summary>
    /// A two-component vector, in metres. In the extras it is the size or centre of a Space Setup plane on the plane's
    /// own x and y axes (the rectangle the headset reports for a wall, a floor or a table top).
    /// </summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        /// <summary>Creates a vector from its two components.</summary>
        /// <param name="x">Component along the first axis.</param>
        /// <param name="y">Component along the second axis.</param>
        public Vec2(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>The component along the first axis.</summary>
        public double X { get; }

        /// <summary>The component along the second axis.</summary>
        public double Y { get; }

        /// <summary>True when both components are real numbers (neither is NaN or infinite).</summary>
        public bool IsFinite
        {
            get { return double.IsFinite(X) && double.IsFinite(Y); }
        }

        /// <summary>True when both components are equal. Two NaN components count as equal.</summary>
        public bool Equals(Vec2 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Vec2 other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((X == 0.0 ? 0.0 : X).GetHashCode() * 31) + (Y == 0.0 ? 0.0 : Y).GetHashCode();
            }
        }

        /// <summary>The vector as "(x, y)" with a period as the decimal separator, whatever the current culture.</summary>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0}, {1})", X.ToString("R", CultureInfo.InvariantCulture), Y.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
