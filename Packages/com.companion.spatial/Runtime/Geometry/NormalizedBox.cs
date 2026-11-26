using System;
using System.Globalization;

namespace Companion.Spatial
{
    /// <summary>
    /// A detection's 2D box as fractions of the image: <c>x1, y1</c> is the top-left corner and <c>x2, y2</c> the
    /// bottom-right, each between 0 and 1 with the origin at the top left. A box is always a fraction of the image,
    /// never pixels: the scanner's old pixel-or-normalized guess read a box with x2 = 1.0001 as pixels and made it a
    /// speck, whereas here <see cref="TryCreate"/> clamps it to the image. The web Scan contract's
    /// <c>bbox_cxcywh</c> is <see cref="ToCxCyWh"/>; the scanner's <c>bbox_normalized</c> (x, y, width, height from the
    /// top left) is <see cref="TryFromXywh"/>.
    /// </summary>
    public readonly struct NormalizedBox : IEquatable<NormalizedBox>
    {
        private readonly double _x1;
        private readonly double _y1;
        private readonly double _x2;
        private readonly double _y2;

        /// <summary>
        /// Creates a box from two corners given as fractions of the image: the corners are put in order and clamped to
        /// 0..1. Use <see cref="TryCreate"/> for values that may be bad.
        /// </summary>
        /// <exception cref="ArgumentException">The box has no area inside the image, or a value is NaN or infinite.</exception>
        public NormalizedBox(double x1, double y1, double x2, double y2)
        {
            if (!TryNormalize(x1, y1, x2, y2, out _x1, out _y1, out _x2, out _y2))
            {
                throw new ArgumentException("A box needs finite corners and some area inside the image: " + x1 + ", " + y1 + ", " + x2 + ", " + y2 + ".");
            }
        }

        /// <summary>The left edge, as a fraction of the image width (0 to 1).</summary>
        public double X1
        {
            get { return _x1; }
        }

        /// <summary>The top edge, as a fraction of the image height (0 to 1, from the top).</summary>
        public double Y1
        {
            get { return _y1; }
        }

        /// <summary>The right edge, as a fraction of the image width (0 to 1).</summary>
        public double X2
        {
            get { return _x2; }
        }

        /// <summary>The bottom edge, as a fraction of the image height (0 to 1, from the top).</summary>
        public double Y2
        {
            get { return _y2; }
        }

        /// <summary>The width as a fraction of the image width.</summary>
        public double Width
        {
            get { return _x2 - _x1; }
        }

        /// <summary>The height as a fraction of the image height.</summary>
        public double Height
        {
            get { return _y2 - _y1; }
        }

        /// <summary>The horizontal centre as a fraction of the image width.</summary>
        public double CenterX
        {
            get { return (_x1 + _x2) / 2.0; }
        }

        /// <summary>The vertical centre as a fraction of the image height (from the top).</summary>
        public double CenterY
        {
            get { return (_y1 + _y2) / 2.0; }
        }

        /// <summary>
        /// Makes a box from two corners given as fractions of the image. The corners are put in order (a reversed pair
        /// is accepted) and each value is clamped to 0..1. Returns false, with a default box, when a value is NaN or
        /// infinite or the clamped box has no width or no height.
        /// </summary>
        public static bool TryCreate(double x1, double y1, double x2, double y2, out NormalizedBox box)
        {
            double nx1, ny1, nx2, ny2;
            if (!TryNormalize(x1, y1, x2, y2, out nx1, out ny1, out nx2, out ny2))
            {
                box = default;
                return false;
            }

            box = new NormalizedBox(nx1, ny1, nx2, ny2);
            return true;
        }

        /// <summary>
        /// Makes a box from its centre and size, all fractions of the image, as in the web contract's
        /// <c>bbox_cxcywh</c> = [cx, cy, w, h]. The size must be positive; the box is then clamped to the image.
        /// </summary>
        public static bool TryFromCxCyWh(double cx, double cy, double w, double h, out NormalizedBox box)
        {
            if (!(w > 0.0) || !(h > 0.0))
            {
                box = default;
                return false;
            }

            return TryCreate(cx - w / 2.0, cy - h / 2.0, cx + w / 2.0, cy + h / 2.0, out box);
        }

        /// <summary>
        /// Makes a box from its top-left corner and size, all fractions of the image, as the scanner's
        /// <c>bbox_normalized</c> = [x, y, width, height]. The size must be positive; the box is then clamped to the image.
        /// </summary>
        public static bool TryFromXywh(double x, double y, double w, double h, out NormalizedBox box)
        {
            if (!(w > 0.0) || !(h > 0.0))
            {
                box = default;
                return false;
            }

            return TryCreate(x, y, x + w, y + h, out box);
        }

        /// <summary>The box as the web contract's <c>bbox_cxcywh</c>: [centre x, centre y, width, height], fractions of the image.</summary>
        public double[] ToCxCyWh()
        {
            return new[] { CenterX, CenterY, Width, Height };
        }

        /// <summary>True when all four edges are equal.</summary>
        public bool Equals(NormalizedBox other)
        {
            return _x1.Equals(other._x1) && _y1.Equals(other._y1) && _x2.Equals(other._x2) && _y2.Equals(other._y2);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is NormalizedBox other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _x1.GetHashCode();
                hash = hash * 31 + _y1.GetHashCode();
                hash = hash * 31 + _x2.GetHashCode();
                hash = hash * 31 + _y2.GetHashCode();
                return hash;
            }
        }

        /// <summary>The box as "[x1, y1, x2, y2]" with a period as the decimal separator, whatever the current culture.</summary>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "[{0}, {1}, {2}, {3}]", _x1.ToString("R", CultureInfo.InvariantCulture), _y1.ToString("R", CultureInfo.InvariantCulture), _x2.ToString("R", CultureInfo.InvariantCulture), _y2.ToString("R", CultureInfo.InvariantCulture));
        }

        private static bool TryNormalize(double x1, double y1, double x2, double y2, out double nx1, out double ny1, out double nx2, out double ny2)
        {
            nx1 = ny1 = nx2 = ny2 = 0.0;
            if (!double.IsFinite(x1) || !double.IsFinite(y1) || !double.IsFinite(x2) || !double.IsFinite(y2))
            {
                return false;
            }

            nx1 = Clamp01(Math.Min(x1, x2));
            nx2 = Clamp01(Math.Max(x1, x2));
            ny1 = Clamp01(Math.Min(y1, y2));
            ny2 = Clamp01(Math.Max(y1, y2));
            return nx2 - nx1 > 0.0 && ny2 - ny1 > 0.0;
        }

        private static double Clamp01(double value)
        {
            return value < 0.0 ? 0.0 : (value > 1.0 ? 1.0 : value);
        }
    }
}
