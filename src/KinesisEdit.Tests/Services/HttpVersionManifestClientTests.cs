using System.Net;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The published-versions GET of specs/09-firmware.md §3 step 3. Every failure has to leave as
    /// an exception, because step 6 turns any exception into the same "Check connection" outcome —
    /// except a cancelled request, which the dialog must be able to tell apart from a failure. The
    /// requests are served by a stub handler — no test reaches the network — and every
    /// <see cref="HttpClient"/> a test creates is disposed with it, because the client under test
    /// owns only the one it built itself.
    /// </summary>
    public class HttpVersionManifestClientTests : IDisposable
    {
        private const string Endpoint = "https://gaming.kinesis-ergo.com/wp-json/ksv/v1/get_versions";
        private const int TimeoutSeconds = 15;

        private readonly List<HttpClient> _httpClients = [];

        [Fact]
        public async Task FetchAsync_WithAJsonBody_ReturnsTheParsedManifest()
        {
            var handler = new FakeHttpMessageHandler
            {
                Body = """{"keyboard_ver":"1.0.121","lighting_ver":"1.0.58"}"""
            };

            using var client = CreateClient(handler);

            var manifest = await client.FetchAsync(Endpoint, CancellationToken.None);

            Assert.Equal("1.0.121", manifest.KeyboardVersion);
            Assert.Equal("1.0.58", manifest.LightingVersion);
            Assert.Equal(handler.Body, manifest.RawJson);
            Assert.Equal(new Uri(Endpoint), Assert.Single(handler.RequestedUris));
        }

        [Fact]
        public async Task FetchAsync_OnAnErrorStatus_Throws()
        {
            var handler = new FakeHttpMessageHandler
            {
                StatusCode = HttpStatusCode.InternalServerError
            };

            using var client = CreateClient(handler);

            await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchAsync(Endpoint, CancellationToken.None));
        }

        [Fact]
        public async Task FetchAsync_OnATransportFailure_Throws()
        {
            var handler = new FakeHttpMessageHandler
            {
                ExceptionToThrow = new HttpRequestException("No such host is known.")
            };

            using var client = CreateClient(handler);

            var exception = await Assert.ThrowsAsync<HttpRequestException>(
                () => client.FetchAsync(Endpoint, CancellationToken.None));

            Assert.Equal("No such host is known.", exception.Message);
        }

        [Theory]
        [InlineData("not json at all")]
        [InlineData("[1, 2, 3]")]
        [InlineData("")]
        public async Task FetchAsync_WithABodyThatIsNotAJsonObject_Throws(string body)
        {
            var handler = new FakeHttpMessageHandler
            {
                Body = body
            };

            using var client = CreateClient(handler);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.FetchAsync(Endpoint, CancellationToken.None));

            Assert.Equal(HttpVersionManifestClient.MalformedResponseMessage, exception.Message);
        }

        [Fact]
        public async Task FetchAsync_WithoutAnEndpoint_Throws()
        {
            using var client = CreateClient(new FakeHttpMessageHandler());

            await Assert.ThrowsAnyAsync<ArgumentException>(() => client.FetchAsync(" ", CancellationToken.None));
        }

        [Fact]
        public async Task FetchAsync_AfterDisposal_Throws()
        {
            var client = CreateClient(new FakeHttpMessageHandler());
            client.Dispose();
            client.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => client.FetchAsync(Endpoint, CancellationToken.None));
        }

        [Fact]
        public async Task FetchAsync_CalledTwice_ReusesTheSameHttpClient()
        {
            var handler = new FakeHttpMessageHandler();

            using var client = CreateClient(handler);

            await client.FetchAsync(Endpoint, CancellationToken.None);
            await client.FetchAsync(Endpoint, CancellationToken.None);

            Assert.Equal(2, handler.RequestedUris.Count);
        }

        [Fact]
        public async Task FetchAsync_WithACancelledToken_ThrowsCancellationRatherThanSending()
        {
            // Step 6 maps every exception to "Check connection", so the dialog separates the two
            // by the cancellation it requested itself — the client must not swallow it.
            var handler = new FakeHttpMessageHandler();

            using var client = CreateClient(handler);
            using var cancellation = new CancellationTokenSource();

            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.FetchAsync(Endpoint, cancellation.Token));

            Assert.Empty(handler.RequestedUris);
        }

        [Fact]
        public async Task FetchAsync_SendsAProductUserAgent()
        {
            // Both endpoints are WordPress sites; a WAF rejecting agent-less requests would turn
            // every check into the step 6 failure.
            var handler = new FakeHttpMessageHandler();

            using var client = CreateClient(handler);

            await client.FetchAsync(Endpoint, CancellationToken.None);

            var userAgent = Assert.Single(handler.RequestedUserAgents);

            Assert.StartsWith(HttpVersionManifestClient.UserAgentProduct + "/", userAgent);
            Assert.Equal(HttpVersionManifestClient.UserAgent, userAgent);
        }

        [Fact]
        public void CreateDefaultHttpClient_ForTheShippedClient_AppliesTheFailFastTimeout()
        {
            using var httpClient = HttpVersionManifestClient.CreateDefaultHttpClient();

            Assert.Equal(TimeSpan.FromSeconds(TimeoutSeconds), httpClient.Timeout);
            Assert.Equal(TimeSpan.FromSeconds(TimeoutSeconds), HttpVersionManifestClient.DefaultTimeout);
        }

        [Fact]
        public void PooledConnectionLifetime_ForTheSharedClient_IsFiniteAndShort()
        {
            // The client lives as long as the process; with the infinite default lifetime it would
            // keep resolving to a stale IP after a network change until the app restarts.
            Assert.NotEqual(Timeout.InfiniteTimeSpan, HttpVersionManifestClient.PooledConnectionLifetime);
            Assert.InRange(
                HttpVersionManifestClient.PooledConnectionLifetime,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromMinutes(10));
        }

        /// <summary>Disposes the caller-owned <see cref="HttpClient"/>s the tests created.</summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);

            foreach (var httpClient in _httpClients)
            {
                httpClient.Dispose();
            }
        }

        private HttpVersionManifestClient CreateClient(FakeHttpMessageHandler handler)
        {
            // The client under test disposes only an HttpClient it created itself, so a test's own
            // one — and the stub handler inside it — is disposed here instead.
            var httpClient = new HttpClient(handler);

            _httpClients.Add(httpClient);

            return new HttpVersionManifestClient(httpClient);
        }
    }
}
