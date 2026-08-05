namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// Fetches the published-versions payload of specs/09-firmware.md §3 step 3 (a plain HTTP GET
    /// against a <see cref="VersionEndpoints"/> URL). Declared here, implemented in the app layer:
    /// <c>KinesisEdit.Core</c> has no HTTP stack and no package references.
    /// </summary>
    public interface IVersionManifestClient
    {
        /// <summary>
        /// GETs <paramref name="endpointUrl"/> and parses the body with
        /// <see cref="VersionManifest.TryParse"/>. Implementations throw on any failure —
        /// transport error, non-success status code, cancellation, or a body that is not a JSON
        /// object — because §3 step 6 maps every exception to the same outcome:
        /// <see cref="UpdateRowState.ConnectionError"/> on all rows. Never returns null.
        /// </summary>
        Task<VersionManifest> FetchAsync(string endpointUrl, CancellationToken cancellationToken);
    }
}
