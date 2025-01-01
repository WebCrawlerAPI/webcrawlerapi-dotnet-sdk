using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Linq;
using WebCrawlerApi.Models;

namespace WebCrawlerApi
{
    public class WebCrawlerApiClient : IDisposable
    {
        private const int DefaultPollDelaySeconds = 5;
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _version;
        private readonly bool _disposeHttpClient;

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
                blacklist_regexp = blacklistRegexp
            };

            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/{_version}/crawl", payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            
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
                    MarkdownContentUrl = item.MarkdownContentUrl
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

        public void Dispose()
        {
            if (_disposeHttpClient)
            {
                _httpClient.Dispose();
            }
        }
    }
} 