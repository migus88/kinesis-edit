using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The app layer's <see cref="IVersionManifestClient"/>: the plain HTTP GET of
    /// specs/09-firmware.md §3 step 3 against a <see cref="VersionEndpoints"/> URL. Every failure
    /// — transport error, non-success status, or a body that is not a JSON object — leaves as an
    /// exception, because §3 step 6 maps any exception to the same outcome (all rows
    /// <see cref="UpdateRowState.ConnectionError"/> plus a dialog quoting the message).
    /// One <see cref="HttpClient"/> is reused for every call — creating one per request is the
    /// classic way to exhaust sockets — over a handler that recycles pooled connections every
    /// <see cref="PooledConnectionLifetime"/>, which is the other half of that trade-off: a
    /// process-lifetime client with the default infinite lifetime would keep a stale IP after a
    /// network change and report "Check connection" until the app is restarted.
    /// </summary>
    public sealed class HttpVersionManifestClient : IVersionManifestClient, IDisposable
    {
        /// <summary>Message of the exception thrown when the endpoint answers with something that is not a JSON object.</summary>
        public const string MalformedResponseMessage = "The firmware version service returned a malformed response.";

        /// <summary>Product name of the <c>User-Agent</c> every request carries.</summary>
        public const string UserAgentProduct = "KinesisEdit";

        /// <summary>Request timeout; the dialog must fail fast rather than hang on a dead endpoint.</summary>
        public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// How long a pooled connection is reused before it is re-established — and its host
        /// re-resolved. Both endpoints are ordinary websites behind DNS that can move, and a
        /// laptop changes networks far more often than it restarts this app.
        /// </summary>
        public static TimeSpan PooledConnectionLifetime { get; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// The <c>User-Agent</c> requests are sent with, as product/version. Both endpoints are
        /// WordPress sites, and a WAF that rejects agent-less requests would turn every check into
        /// the §3 step 6 failure.
        /// </summary>
        public static string UserAgent { get; } = BuildUserAgent();

        /// <summary>
        /// Builds the <see cref="HttpClient"/> the parameterless constructor uses: the fail-fast
        /// timeout, the product agent, and a handler that drops pooled connections often enough to
        /// pick up DNS changes.
        /// </summary>
        public static HttpClient CreateDefaultHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = PooledConnectionLifetime
            };

            return new HttpClient(handler, disposeHandler: true)
            {
                Timeout = DefaultTimeout
            };
        }

        private static string BuildUserAgent()
        {
            var version = AssemblyAppVersionProvider.Format(typeof(HttpVersionManifestClient).Assembly.GetName().Version);

            return version.Length == 0 ? UserAgentProduct : UserAgentProduct + "/" + version;
        }

        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private bool _isDisposed;

        /// <summary>Creates the client over its own <see cref="HttpClient"/>, which it disposes.</summary>
        public HttpVersionManifestClient() : this(CreateDefaultHttpClient(), ownsHttpClient: true)
        {
        }

        /// <summary>
        /// Creates the client over a caller-owned <paramref name="httpClient"/> — the seam the
        /// tests drive with a stub handler, so no test ever reaches the network.
        /// </summary>
        public HttpVersionManifestClient(HttpClient httpClient) : this(httpClient, ownsHttpClient: false)
        {
        }

        private HttpVersionManifestClient(HttpClient httpClient, bool ownsHttpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _ownsHttpClient = ownsHttpClient;

            if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                // Set here rather than in CreateDefaultHttpClient so a caller-owned client — the
                // tests' — sends the same agent the shipped one does, and so a client that already
                // carries an agent keeps it.
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            }
        }

        /// <summary>
        /// GETs <paramref name="endpointUrl"/> and parses the body with
        /// <see cref="VersionManifest.TryParse"/>. Throws on any failure; never returns null.
        /// </summary>
        public async Task<VersionManifest> FetchAsync(string endpointUrl, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(endpointUrl);
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            using var response = await _httpClient.GetAsync(endpointUrl, cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!VersionManifest.TryParse(body, out var manifest))
            {
                throw new InvalidOperationException(MalformedResponseMessage);
            }

            return manifest;
        }

        /// <summary>Disposes the <see cref="HttpClient"/> when this instance created it. Safe to call multiple times.</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
        }
    }
}
