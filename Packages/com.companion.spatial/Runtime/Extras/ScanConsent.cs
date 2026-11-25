namespace Companion.Spatial.Extras
{
    /// <summary>
    /// The user's agreement to the scan, recorded in the file so it travels with the data: which version of the notice
    /// they agreed to, when, and the server the data goes to. The scanner asks first and writes this before any upload;
    /// the server checks it too. The web Scan contract's consent records a camera opt-in with <c>granted_at</c>.
    /// </summary>
    public sealed class ScanConsent : ExtrasNode
    {
        /// <summary>The version of the privacy notice the user agreed to (JSON <c>notice_version</c>).</summary>
        public string NoticeVersion { get; set; }

        /// <summary>When the user agreed, as an ISO 8601 UTC time (JSON <c>agreed_at_utc</c>).</summary>
        public string AgreedAtUtc { get; set; }

        /// <summary>The server the user agreed the data may go to, such as its base URL; null when the file stays on the device (JSON <c>server</c>).</summary>
        public string Server { get; set; }
    }
}
