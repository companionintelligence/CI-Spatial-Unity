namespace Companion.Spatial.Extras
{
    /// <summary>
    /// The pinhole model of a camera frame, in pixels of that frame's image: the focal lengths and the principal
    /// point. The scanner keeps them in <c>CaptureFrame.cameraIntrinsics</c> (<c>m00</c> = fx, <c>m11</c> = fy,
    /// <c>m02</c> = cx, <c>m12</c> = cy); the web Scan contract's <c>Intrinsics</c> has the same four numbers.
    /// </summary>
    public sealed class ScanIntrinsics : ExtrasNode
    {
        /// <summary>The focal length along the image's x axis, in pixels (above 0).</summary>
        public double Fx { get; set; }

        /// <summary>The focal length along the image's y axis, in pixels (above 0).</summary>
        public double Fy { get; set; }

        /// <summary>The x coordinate of the principal point, in pixels.</summary>
        public double Cx { get; set; }

        /// <summary>The y coordinate of the principal point, in pixels.</summary>
        public double Cy { get; set; }

        /// <summary>The width of the image the numbers refer to, in pixels; 0 when unknown (then it is not written).</summary>
        public int Width { get; set; }

        /// <summary>The height of the image the numbers refer to, in pixels; 0 when unknown (then it is not written).</summary>
        public int Height { get; set; }
    }
}
