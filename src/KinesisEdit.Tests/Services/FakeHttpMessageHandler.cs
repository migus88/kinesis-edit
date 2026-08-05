using System.Net;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="HttpMessageHandler"/> answering every request from
    /// <see cref="StatusCode"/> and <see cref="Body"/>, or throwing
    /// <see cref="ExceptionToThrow"/>. It is what keeps the HTTP client's tests off the network.
    /// </summary>
    internal sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        public string Body { get; set; } = "{}";

        public Exception? ExceptionToThrow { get; set; }

        public List<Uri?> RequestedUris { get; } = [];

        /// <summary>The User-Agent header of every request, in order; empty when a request carried none.</summary>
        public List<string> RequestedUserAgents { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // A real handler observes the token before it does anything; the client's
            // "cancelled is not a failure" contract depends on it.
            cancellationToken.ThrowIfCancellationRequested();

            RequestedUris.Add(request.RequestUri);
            RequestedUserAgents.Add(request.Headers.UserAgent.ToString());

            if (ExceptionToThrow is not null)
            {
                return Task.FromException<HttpResponseMessage>(ExceptionToThrow);
            }

            return Task.FromResult(new HttpResponseMessage(StatusCode)
            {
                Content = new StringContent(Body)
            });
        }
    }
}
