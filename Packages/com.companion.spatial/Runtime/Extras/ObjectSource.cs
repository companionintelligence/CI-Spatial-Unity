namespace Companion.Spatial.Extras
{
    /// <summary>Who found an object: written as <c>"cv"</c>, <c>"device"</c> or <c>"manual"</c>.</summary>
    public enum ObjectSource
    {
        /// <summary>A computer-vision detector found it in a camera frame (the scanner's <c>PinSource.CvServer</c>).</summary>
        Cv,

        /// <summary>The headset's own scene model reported it, for example a Space Setup anchor (<c>PinSource.MetaScene</c>).</summary>
        Device,

        /// <summary>The user placed or edited it by hand.</summary>
        Manual
    }
}
