using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Linq;
using System.Diagnostics;
using WebCrawlerApi.Models;

namespace WebCrawlerApi
{
    public class WebCrawlerApiClient : IDisposable
    {
        private const int DefaultPollDelaySeconds = 5;
        private const int DefaultScrapeMaxPolls = 1000;
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _version;
        private readonly bool _disposeHttpClient;

        /// <summary>Maximum total time <see cref="ScrapeAndWaitAsync"/> waits for a scrape. Default 400 seconds.</summary>
        public TimeSpan ScrapeMaxWait { get; set; } = TimeSpan.FromSeconds(400);

        /// <summary>Poll delay used by <see cref="ScrapeAndWaitAsync"/> when the server does not recommend one. Default 2 seconds.</summary>
        public TimeSpan ScrapePollDelay { get; set; } = TimeSpan.FromSeconds(2);

        public WebCrawlerApiClient(string apiKey, string baseUrl = "https://api.webcrawlerapi.com", string version = "v1", HttpClient? httpClient = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _version = version;
            
            if (httpClient != null)
            {
                _httpClient = httpClient;
                _disposeHttpClient = false;
            }
            else
            {
                _httpClient = new HttpClient();
                _disposeHttpClient = true;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<CrawlResponse> CrawlAsync(
            string url,
            string scrapeType = "html",
            int itemsLimit = 10,
            string? webhookUrl = null,
            bool allowSubdomains = false,
            string? whitelistRegexp = null,
            string? blacklistRegexp = null,
            bool mainContentOnly = false,
            CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                url,
                scrape_type = scrapeType,
                items_limit = itemsLimit,
                allow_subdomains = allowSubdomains,
                webhook_url = webhookUrl,
                whitelist_regexp = whitelistRegexp,
                blacklist_regexp = blacklistRegexp,
                main_content_only = mainContentOnly
            };

            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/{_version}/crawl", payload, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = await response.Content.ReadFromJsonAsync<CrawlResponse>(cancellationToken: cancellationToken);
                if (errorResponse != null)
                {
                    return errorResponse;
                }
                
                throw new HttpRequestException($"Request failed with status code {response.StatusCode}");
            }
            
            return await response.Content.ReadFromJsonAsync<CrawlResponse>(cancellationToken: cancellationToken) 
                   ?? throw new JsonException("Failed to deserialize response");
        }

        public async Task<Job> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/{_version}/job/{jobId}", cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var job = await response.Content.ReadFromJsonAsync<Job>(cancellationToken: cancellationToken) 
                   ?? throw new JsonException("Failed to deserialize response");

            // Initialize JobItems with references to the job and HttpClient
            var initializedItems = job.JobItems.Select(item =>
            {
                var newItem = new JobItem(job, _httpClient)
                {
                    Id = item.Id,
                    JobId = item.JobId,
                    OriginalUrl = item.OriginalUrl,
                    PageStatusCode = item.PageStatusCode,
                    Status = item.Status,
                    Title = item.Title,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt,
                    Cost = item.Cost,
                    ReferredUrl = item.ReferredUrl,
                    LastError = item.LastError,
                    RawContentUrl = item.RawContentUrl,
                    CleanedContentUrl = item.CleanedContentUrl,
                    MarkdownContentUrl = item.MarkdownContentUrl,
                    Links = item.Links
                };
                return newItem;
            }).ToList();

            // Create a new job with the initialized items
            job.JobItems = initializedItems;
            return job;
        }

        public async Task<string> CancelJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PutAsync($"{_baseUrl}/{_version}/job/{jobId}/cancel", null, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            return result.GetProperty("message").GetString() ?? "Job cancelled";
        }

        public async Task<Job> CrawlAndWaitAsync(
            string url,
            string scrapeType = "html",
            int itemsLimit = 10,
            string? webhookUrl = null,
            bool allowSubdomains = false,
            string? whitelistRegexp = null,
            string? blacklistRegexp = null,
            bool mainContentOnly = false,
            int maxPolls = 100,
            CancellationToken cancellationToken = default)
        {
            var response = await CrawlAsync(
                url,
                scrapeType,
                itemsLimit,
                webhookUrl,
                allowSubdomains,
                whitelistRegexp,
                blacklistRegexp,
                mainContentOnly,
                cancellationToken);

            var polls = 0;
            Job job;

            while (polls < maxPolls)
            {
                job = await GetJobAsync(response.Id, cancellationToken);

                if (job.IsTerminal)
                    return job;

                var delayMs = job.RecommendedPullDelayMs > 0 
                    ? job.RecommendedPullDelayMs 
                    : DefaultPollDelaySeconds * 1000;

                await Task.Delay(delayMs, cancellationToken);
                polls++;
            }

            // Return the last known state if max_polls is reached
            return await GetJobAsync(response.Id, cancellationToken);
        }

        public async Task<ScrapeId> ScrapeAsync(
            ScrapeRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/v2/scrape?async=true", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = await response.Content.ReadFromJsonAsync<ScrapeResponse>(cancellationToken: cancellationToken);
                if (errorResponse?.ErrorMessage != null)
                {
                    throw new HttpRequestException($"Scrape request failed: {errorResponse.ErrorMessage}");
                }

                throw new HttpRequestException($"Request failed with status code {response.StatusCode}");
            }

            return await response.Content.ReadFromJsonAsync<ScrapeId>(cancellationToken: cancellationToken)
                   ?? throw new JsonException("Failed to deserialize scrape ID");
        }

        public async Task<ScrapeResponse> GetScrapeAsync(
            string scrapeId,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/v2/scrape/{scrapeId}", cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ScrapeResponse>(cancellationToken: cancellationToken)
                   ?? throw new JsonException("Failed to deserialize scrape response");
        }

        /// <summary>
        /// Starts a scrape asynchronously and polls until it is done or failed.
        /// Mirrors the JS SDK <c>scrape()</c>: waits up to <see cref="ScrapeMaxWait"/> (400s by default) and
        /// <paramref name="maxPolls"/> polls, sleeps for the server-recommended delay
        /// (<c>recommended_pull_delay_ms</c>, falling back to <see cref="ScrapePollDelay"/>), returns the
        /// result as soon as it is done or carries an error code, and returns the last known (in-progress)
        /// state on timeout instead of throwing.
        /// </summary>
        public async Task<ScrapeResponse> ScrapeAndWaitAsync(
            ScrapeRequest request,
            int maxPolls = DefaultScrapeMaxPolls,
            CancellationToken cancellationToken = default)
        {
            var scrapeId = await ScrapeAsync(request, cancellationToken);

            var stopwatch = Stopwatch.StartNew();
            var polls = 0;
            ScrapeResponse? result = null;

            // Bounded by both elapsed time (ScrapeMaxWait) and maxPolls
            while (polls < maxPolls && stopwatch.Elapsed < ScrapeMaxWait)
            {
                result = await GetScrapeAsync(scrapeId.Id, cancellationToken);

                if (result.Status == "done" || result.Success || result.ErrorCode != null)
                {
                    return result;
                }

                // Use the server-recommended delay when present, otherwise the default
                var delay = result.RecommendedPullDelayMs > 0
                    ? TimeSpan.FromMilliseconds(result.RecommendedPullDelayMs)
                    : ScrapePollDelay;

                // Don't sleep past the overall deadline
                var remaining = ScrapeMaxWait - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                await Task.Delay(delay < remaining ? delay : remaining, cancellationToken);
                polls++;
            }

            // Return the last known state on timeout
            return result ?? await GetScrapeAsync(scrapeId.Id, cancellationToken);
        }

        /// <summary>
        /// Batch overload of <see cref="ScrapeAndWaitAsync(ScrapeRequest,int,CancellationToken)"/>: creates a batch
        /// for a list of URLs, polls until it finishes (or <see cref="ScrapeMaxWait"/> / <paramref name="maxPolls"/>
        /// is hit, then returns the last known state) and returns progress plus the first page of items
        /// (<c>Next</c> is the cursor for more, see <see cref="GetBatchAsync"/>).
        /// </summary>
        public async Task<BatchScrapeResponse> ScrapeAndWaitAsync(
            IEnumerable<string> urls,
            BatchScrapeOptions? options = null,
            int maxPolls = DefaultScrapeMaxPolls,
            CancellationToken cancellationToken = default)
        {
            var created = await ScrapeBatchAsync(urls, options, cancellationToken);

            var stopwatch = Stopwatch.StartNew();
            var polls = 0;
            BatchScrapeResponse? result = null;

            while (polls < maxPolls && stopwatch.Elapsed < ScrapeMaxWait)
            {
                result = await GetBatchAsync(created.Id, cancellationToken: cancellationToken);
                if (result.IsTerminal)
                {
                    return result;
                }

                var remaining = ScrapeMaxWait - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                await Task.Delay(ScrapePollDelay < remaining ? ScrapePollDelay : remaining, cancellationToken);
                polls++;
            }

            return result ?? await GetBatchAsync(created.Id, cancellationToken: cancellationToken);
        }

        /// <summary>Creates a batch scrape without waiting (POST /v2/batch/scrape). Returns the batch id and rejected URLs.</summary>
        public async Task<BatchScrapeCreated> ScrapeBatchAsync(
            IEnumerable<string> urls,
            BatchScrapeOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var o = options ?? new BatchScrapeOptions();
            var payload = new Dictionary<string, object?> { ["urls"] = urls.ToArray() };
            if (o.OutputFormats != null) payload["output_formats"] = o.OutputFormats;
            if (o.MainContentOnly != null) payload["main_content_only"] = o.MainContentOnly;
            if (o.CleanSelectors != null) payload["clean_selectors"] = o.CleanSelectors;
            if (o.MaxConcurrency != null) payload["max_concurrency"] = o.MaxConcurrency;
            if (o.IgnoreInvalidUrls != null) payload["ignore_invalid_urls"] = o.IgnoreInvalidUrls;
            if (o.WebhookUrl != null) payload["webhook_url"] = o.WebhookUrl;
            if (o.MaxAge != null) payload["max_age"] = o.MaxAge;
            if (o.RespectRobotsTxt != null) payload["respect_robots_txt"] = o.RespectRobotsTxt;

            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/v2/batch/scrape", payload, cancellationToken);
            await EnsureBatchSuccessAsync(response, "Batch scrape request", cancellationToken);

            return await response.Content.ReadFromJsonAsync<BatchScrapeCreated>(cancellationToken: cancellationToken)
                   ?? throw new JsonException("Failed to deserialize batch scrape response");
        }

        /// <summary>Gets batch progress and one page of items (GET /v2/batch/scrape/:id). Pass the previous <c>Next</c> as <paramref name="cursor"/>.</summary>
        public async Task<BatchScrapeResponse> GetBatchAsync(
            string batchId,
            int? limit = null,
            string? cursor = null,
            CancellationToken cancellationToken = default)
        {
            var query = new List<string>();
            if (limit != null) query.Add($"limit={limit}");
            if (!string.IsNullOrEmpty(cursor)) query.Add($"cursor={Uri.EscapeDataString(cursor)}");
            var qs = query.Count > 0 ? "?" + string.Join("&", query) : "";

            var response = await _httpClient.GetAsync($"{_baseUrl}/v2/batch/scrape/{batchId}{qs}", cancellationToken);
            await EnsureBatchSuccessAsync(response, "Batch status request", cancellationToken);

            return await response.Content.ReadFromJsonAsync<BatchScrapeResponse>(cancellationToken: cancellationToken)
                   ?? throw new JsonException("Failed to deserialize batch scrape status");
        }

        /// <summary>Lists failed URLs and robots.txt-blocked URLs of a batch (GET /v2/batch/scrape/:id/errors).</summary>
        public async Task<BatchScrapeErrorsResponse> GetBatchErrorsAsync(string batchId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/v2/batch/scrape/{batchId}/errors", cancellationToken);
            await EnsureBatchSuccessAsync(response, "Batch errors request", cancellationToken);

            return await response.Content.ReadFromJsonAsync<BatchScrapeErrorsResponse>(cancellationToken: cancellationToken)
                   ?? throw new JsonException("Failed to deserialize batch errors");
        }

        /// <summary>Cancels a running batch (DELETE /v2/batch/scrape/:id). Returns the resulting status.</summary>
        public async Task<string> CancelBatchAsync(string batchId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/v2/batch/scrape/{batchId}", cancellationToken);
            await EnsureBatchSuccessAsync(response, "Batch cancel request", cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            return result.TryGetProperty("status", out var status) ? status.GetString() ?? "canceled" : "canceled";
        }

        private static async Task EnsureBatchSuccessAsync(HttpResponseMessage response, string what, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode) return;

            string? message = null;
            try
            {
                var err = await response.Content.ReadFromJsonAsync<ScrapeResponse>(cancellationToken: cancellationToken);
                message = err?.ErrorMessage;
            }
            catch (JsonException) { }

            throw new HttpRequestException(message != null
                ? $"{what} failed: {message}"
                : $"{what} failed with status code {(int)response.StatusCode}");
        }

        public void Dispose()
        {
            if (_disposeHttpClient)
            {
                _httpClient.Dispose();
            }
        }
    }
} 