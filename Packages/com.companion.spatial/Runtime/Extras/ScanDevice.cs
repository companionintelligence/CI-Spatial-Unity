namespace Companion.Spatial.Extras
{
    /// <summary>
    /// The headset or phone and the app that made the scan, so a file can be traced to the build and hardware that
    /// produced it (the web Scan contract calls this the client).
    /// </summary>
    public sealed class ScanDevice : ExtrasNode
    {
        /// <summary>The device model ("Meta Quest 3"; JSON <c>model</c>).</summary>
        public string Model { get; set; }

        /// <summary>The operating system and its version ("Horizon OS 78"; JSON <c>os</c>).</summary>
        public string Os { get; set; }

        /// <summary>The app's name ("companion-scan"; JSON <c>app</c>).</summary>
        public string App { get; set; }

        /// <summary>The app's version ("1.4.0"; JSON <c>app_version</c>).</summary>
        public string AppVersion { get; set; }
    }
}
