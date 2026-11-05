using System;
using System.Collections.Generic;
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
    /// <summary>Offline tests for batch scrape (no network).</summary>
    public class BatchScrapeTests
    {
        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _respond;
            public List<string> Requests { get; } = new();
            public List<string> Bodies { get; } = new();
            public int GetCount { get; private set; }

            public FakeHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) => _respond = respond;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add($"{request.Method} {request.RequestUri!.PathAndQuery}");
                Bodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
                var idx = 0;
                if (request.Method == HttpMethod.Get) { idx = GetCount; GetCount++; }
                return _respond(request, idx);
            }
        }

        private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
            new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private static WebCrawlerApiClient NewClient(FakeHandler h) =>
            new WebCrawlerApiClient("test-key", "https://api.test.com", "v1", new HttpClient(h));

        private const string Created = "{\"id\":\"b1\",\"status_url\":\"https://api.test.com/v2/batch/scrape/b1\",\"invalid_urls\":[{\"url\":\"nope\",\"error_code\":\"invalid_request\",\"error_message\":\"invalid url\"}]}";
        private const string Scraping = "{\"id\":\"b1\",\"status\":\"scraping\",\"total\":2,\"completed\":0,\"failed\":0,\"canceled\":0,\"data\":[],\"next\":\"\"}";
        private const string Completed = "{\"id\":\"b1\",\"status\":\"completed\",\"total\":2,\"completed\":1,\"failed\":1,\"canceled\":0,\"data\":[{\"url\":\"https://a.com\",\"status\":\"done\",\"page_status_code\":200,\"markdown_url\":\"https://d/a.md\"},{\"url\":\"https://b.com\",\"status\":\"error\",\"error_code\":\"webpage_non_success\",\"error_message\":\"x\"}],\"next\":\"cur2\"}";

        [Fact]
        public async Task ScrapeBatchAsyncSendsOptionsAndParsesInvalidUrls()
        {
            var h = new FakeHandler((_, _) => Json(Created));
            using var client = NewClient(h);

            var created = await client.ScrapeBatchAsync(new[] { "https://a.com", "nope" },
                new BatchScrapeOptions { MaxConcurrency = 5, WebhookUrl = "https://hook.test", OutputFormats = new[] { "markdown" } });

            Assert.Equal("b1", created.Id);
            Assert.Single(created.InvalidUrls);
            Assert.Equal("invalid_request", created.InvalidUrls[0].ErrorCode);
            Assert.Equal("POST /v2/batch/scrape", h.Requests[0]);
            Assert.Contains("\"urls\":[\"https://a.com\",\"nope\"]", h.Bodies[0]);
            Assert.Contains("\"max_concurrency\":5", h.Bodies[0]);
            Assert.Contains("\"webhook_url\":\"https://hook.test\"", h.Bodies[0]);
            Assert.DoesNotContain("main_content_only", h.Bodies[0]);
        }

        [Fact]
        public async Task ScrapeAndWaitWithUrlListPollsUntilCompleted()
        {
            var h = new FakeHandler((req, i) =>
                req.Method == HttpMethod.Post ? Json(Created) : i < 2 ? Json(Scraping) : Json(Completed));
            using var client = NewClient(h);
            client.ScrapePollDelay = TimeSpan.FromMilliseconds(10);

            var result = await client.ScrapeAndWaitAsync(new List<string> { "https://a.com", "https://b.com" });

            Assert.Equal("completed", result.Status);
            Assert.Equal(2, result.Data.Count);
            Assert.Equal("cur2", result.Next);
            Assert.Equal(3, h.GetCount);
            Assert.Equal("GET /v2/batch/scrape/b1", h.Requests[1]);
        }

        [Fact]
        public async Task MaxPollsReturnsLastStateWithoutThrowing()
        {
            var h = new FakeHandler((req, _) => req.Method == HttpMethod.Post ? Json(Created) : Json(Scraping));
            using var client = NewClient(h);
            client.ScrapePollDelay = TimeSpan.FromMilliseconds(5);

            var result = await client.ScrapeAndWaitAsync(new[] { "https://a.com" }, maxPolls: 3);

            Assert.Equal("scraping", result.Status);
            Assert.Equal(3, h.GetCount);
        }

        [Fact]
        public async Task GetBatchAsyncSendsLimitAndCursor()
        {
            var h = new FakeHandler((_, _) => Json(Completed));
            using var client = NewClient(h);

            await client.GetBatchAsync("b1", limit: 50, cursor: "a b");

            Assert.Equal("GET /v2/batch/scrape/b1?limit=50&cursor=a%20b", h.Requests[0]);
        }

        [Fact]
        public async Task GetBatchErrorsAsyncParsesErrorsAndRobotsBlocked()
        {
            var h = new FakeHandler((_, _) => Json("{\"errors\":[{\"url\":\"https://b.com\",\"error_code\":\"webpage_non_success\",\"error_message\":\"x\"}],\"robots_blocked\":[\"https://c.com\"]}"));
            using var client = NewClient(h);

            var r = await client.GetBatchErrorsAsync("b1");

            Assert.Equal("GET /v2/batch/scrape/b1/errors", h.Requests[0]);
            Assert.Single(r.Errors);
            Assert.Equal("https://c.com", r.RobotsBlocked[0]);
        }

        [Fact]
        public async Task CancelBatchAsyncSendsDelete()
        {
            var h = new FakeHandler((_, _) => Json("{\"status\":\"canceled\"}"));
            using var client = NewClient(h);

            var status = await client.CancelBatchAsync("b1");

            Assert.Equal("canceled", status);
            Assert.Equal("DELETE /v2/batch/scrape/b1", h.Requests[0]);
        }

        [Fact]
        public async Task ThrowsWithApiMessageOnError()
        {
            var h = new FakeHandler((_, _) => Json("{\"success\":false,\"error_code\":\"invalid_request\",\"error_message\":\"urls is required\"}", HttpStatusCode.BadRequest));
            using var client = NewClient(h);

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.ScrapeBatchAsync(Array.Empty<string>()));

            Assert.Contains("urls is required", ex.Message);
        }
    }
}
