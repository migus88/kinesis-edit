using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="IVersionManifestClient"/>: returns a manifest built from
    /// <see cref="Json"/>, or throws <see cref="ExceptionToThrow"/> when one is set. No test ever
    /// reaches the network.
    /// </summary>
    internal sealed class FakeVersionManifestClient : IVersionManifestClient
    {
        public string Json { get; set; } = "{}";

        public Exception? ExceptionToThrow { get; set; }

        /// <summary>When set, the fetch waits for it — the seam for "a check is still in flight" tests.</summary>
        public TaskCompletionSource? Gate { get; set; }

        public List<string> RequestedUrls { get; } = [];

        public async Task<VersionManifest> FetchAsync(string endpointUrl, CancellationToken cancellationToken)
        {
            RequestedUrls.Add(endpointUrl);

            if (Gate is not null)
            {
                await Gate.Task.ConfigureAwait(false);
            }

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            VersionManifest.TryParse(Json, out var manifest);

            return manifest;
        }
    }
}
