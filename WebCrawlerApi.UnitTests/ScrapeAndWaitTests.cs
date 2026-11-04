using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebCrawlerApi;
using WebCrawlerApi.Models;
using Xunit;

namespace WebCrawlerApi.UnitTests
{
    /// <summary>Offline tests for ScrapeAsync / GetScrapeAsync / ScrapeAndWaitAsync (no network).</summary>
    public class ScrapeAndWaitTests
    {
        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _respond;
            public List<string> Requests { get; } = new();
            public int GetCount { get; private set; }

            public FakeHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) => _respond = respond;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add($"{request.Method} {request.RequestUri!.PathAndQuery}");
                var getIndex = 0;
                if (request.Method == HttpMethod.Get)
                {
                    getIndex = GetCount;
                    GetCount++;
                }
                return Task.FromResult(_respond(request, getIndex));
            }
        }

        private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
            new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private static HttpResponseMessage StartedResponse() => Json("{\"id\":\"scrape-1\"}");

        private static string InProgress(int recommendedMs = 0) =>
            recommendedMs > 0
                ? "{\"status\":\"in_progress\",\"recommended_pull_delay_ms\":" + recommendedMs + "}"
                : "{\"status\":\"in_progress\"}";

        private const string DoneBody = "{\"success\":true,\"status\":\"done\",\"markdown\":\"# Hello\",\"page_status_code\":200}";

        private static WebCrawlerApiClient NewClient(FakeHandler handler) =>
            new WebCrawlerApiClient("test-key", "https://api.test.com", "v1", new HttpClient(handler));

        private static ScrapeRequest Request() => new ScrapeRequest { Url = "https://example.com", OutputFormat = "markdown" };

        [Fact]
        public async Task StartsAsyncScrapeThenPollsUntilDone()
        {
            var handler = new FakeHandler((req, getIndex) =>
                req.Method == HttpMethod.Post ? StartedResponse() : getIndex < 2 ? Json(InProgress(10)) : Json(DoneBody));
            using var client = NewClient(handler);

            var result = await client.ScrapeAndWaitAsync(Request());

            Assert.True(result.Success);
            Assert.Equal("done", result.Status);
            Assert.Equal("# Hello", result.Markdown);
            Assert.Equal("POST /v2/scrape?async=true", handler.Requests[0]);
            Assert.All(handler.Requests.GetRange(1, handler.Requests.Count - 1), r => Assert.Equal("GET /v2/scrape/scrape-1", r));
            Assert.Equal(3, handler.GetCount);
        }

        [Fact]
        public async Task ReturnsImmediatelyWhenFirstPollIsDone()
        {
            var handler = new FakeHandler((req, _) => req.Method == HttpMethod.Post ? StartedResponse() : Json(DoneBody));
            using var client = NewClient(handler);

            var result = await client.ScrapeAndWaitAsync(Request());

            Assert.True(result.Success);
            Assert.Equal(1, handler.GetCount);
        }

        [Fact]
        public async Task UsesServerRecommendedDelayInsteadOfDefault()
        {
            var handler = new FakeHandler((req, getIndex) =>
                req.Method == HttpMethod.Post ? StartedResponse() : getIndex < 3 ? Json(InProgress(20)) : Json(DoneBody));
            using var client = NewClient(handler);
            client.ScrapePollDelay = TimeSpan.FromSeconds(30); // would hang the test if the recommended delay was ignored

            var sw = Stopwatch.StartNew();
            var result = await client.ScrapeAndWaitAsync(Request());
            sw.Stop();

            Assert.True(result.Success);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"took {sw.Elapsed}");
            Assert.Equal(4, handler.GetCount);
        }

        [Fact]
        public async Task FallsBackToDefaultDelayWhenServerDoesNotRecommendOne()
        {
            var handler = new FakeHandler((req, getIndex) =>
                req.Method == HttpMethod.Post ? StartedResponse() : getIndex < 2 ? Json(InProgress()) : Json(DoneBody));
            using var client = NewClient(handler);
            client.ScrapePollDelay = TimeSpan.FromMilliseconds(100);

            var sw = Stopwatch.StartNew();
            var result = await client.ScrapeAndWaitAsync(Request());
            sw.Stop();

            Assert.True(result.Success);
            Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(180), $"took only {sw.Elapsed}");
        }

        [Fact]
        public async Task ReturnsErrorResultImmediately()
        {
            var handler = new FakeHandler((req, getIndex) =>
                req.Method == HttpMethod.Post
                    ? StartedResponse()
                    : getIndex == 0
                        ? Json(InProgress(10))
                        : Json("{\"success\":false,\"status\":\"error\",\"error_code\":\"webpage_non_success\",\"error_message\":\"nope\"}"));
            using var client = NewClient(handler);

            var result = await client.ScrapeAndWaitAsync(Request());

            Assert.False(result.Success);
            Assert.Equal("webpage_non_success", result.ErrorCode);
            Assert.Equal("nope", result.ErrorMessage);
            Assert.Equal(2, handler.GetCount);
        }

        [Fact]
        public async Task ReturnsLastInProgressStateOnTimeoutWithoutThrowing()
        {
            var handler = new FakeHandler((req, _) => req.Method == HttpMethod.Post ? StartedResponse() : Json(InProgress(50)));
            using var client = NewClient(handler);
            client.ScrapeMaxWait = TimeSpan.FromMilliseconds(400);

            var sw = Stopwatch.StartNew();
            var result = await client.ScrapeAndWaitAsync(Request());
            sw.Stop();

            Assert.False(result.Success);
            Assert.Equal("in_progress", result.Status);
            Assert.True(result.IsInProgress);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"took {sw.Elapsed}");
            Assert.True(handler.GetCount >= 2);
        }

        [Fact]
        public async Task MaxPollsLimitsNumberOfStatusChecks()
        {
            var handler = new FakeHandler((req, _) => req.Method == HttpMethod.Post ? StartedResponse() : Json(InProgress(5)));
            using var client = NewClient(handler);

            var result = await client.ScrapeAndWaitAsync(Request(), maxPolls: 3);

            Assert.Equal("in_progress", result.Status);
            Assert.Equal(3, handler.GetCount);
        }

        [Fact]
        public async Task ThrowsWhenStartingTheScrapeFails()
        {
            var handler = new FakeHandler((req, _) =>
                Json("{\"success\":false,\"error_code\":\"invalid_request\",\"error_message\":\"bad url\"}", HttpStatusCode.BadRequest));
            using var client = NewClient(handler);

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.ScrapeAndWaitAsync(Request()));

            Assert.Contains("bad url", ex.Message);
            Assert.Equal(0, handler.GetCount);
        }

        [Fact]
        public async Task CancellationStopsPolling()
        {
            var handler = new FakeHandler((req, _) => req.Method == HttpMethod.Post ? StartedResponse() : Json(InProgress(5000)));
            using var client = NewClient(handler);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ScrapeAndWaitAsync(Request(), cancellationToken: cts.Token));
        }

        [Fact]
        public void DefaultsMatchJsSdk()
        {
            using var client = new WebCrawlerApiClient("test-key");

            Assert.Equal(TimeSpan.FromSeconds(400), client.ScrapeMaxWait);
            Assert.Equal(TimeSpan.FromSeconds(2), client.ScrapePollDelay);
        }
    }
}
