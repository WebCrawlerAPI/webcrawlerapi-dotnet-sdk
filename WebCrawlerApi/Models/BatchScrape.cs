using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace WebCrawlerApi.Models
{
    /// <summary>Optional settings for a batch scrape (POST /v2/batch/scrape). Null properties are omitted.</summary>
    public class BatchScrapeOptions
    {
        [JsonPropertyName("output_formats")]
        public string[]? OutputFormats { get; set; }

        [JsonPropertyName("main_content_only")]
        public bool? MainContentOnly { get; set; }

        [JsonPropertyName("clean_selectors")]
        public string? CleanSelectors { get; set; }

        [JsonPropertyName("max_concurrency")]
        public int? MaxConcurrency { get; set; }

        [JsonPropertyName("ignore_invalid_urls")]
        public bool? IgnoreInvalidUrls { get; set; }

        [JsonPropertyName("webhook_url")]
        public string? WebhookUrl { get; set; }

        [JsonPropertyName("max_age")]
        public int? MaxAge { get; set; }

        [JsonPropertyName("respect_robots_txt")]
        public bool? RespectRobotsTxt { get; set; }
    }

    public class BatchScrapeInvalidUrl
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("error_code")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Result of creating a batch scrape.</summary>
    public class BatchScrapeCreated
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status_url")]
        public string? StatusUrl { get; set; }

        [JsonPropertyName("invalid_urls")]
        public List<BatchScrapeInvalidUrl> InvalidUrls { get; set; } = new();
    }

    public class BatchScrapeItem
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("page_status_code")]
        public int PageStatusCode { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("markdown_url")]
        public string? MarkdownUrl { get; set; }

        [JsonPropertyName("cleaned_url")]
        public string? CleanedUrl { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("from_cache")]
        public bool FromCache { get; set; }

        [JsonPropertyName("error_code")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Batch progress plus one page of items. <see cref="Next"/> is the cursor for the next page (empty on the last page).</summary>
    public class BatchScrapeResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>scraping, completed, failed or cancelled.</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("completed")]
        public int Completed { get; set; }

        [JsonPropertyName("failed")]
        public int Failed { get; set; }

        [JsonPropertyName("canceled")]
        public int Canceled { get; set; }

        [JsonPropertyName("data")]
        public List<BatchScrapeItem> Data { get; set; } = new();

        [JsonPropertyName("next")]
        public string? Next { get; set; }

        public bool IsTerminal => Status == "completed" || Status == "failed" || Status == "cancelled" || Status == "canceled";
    }

    public class BatchScrapeError
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("error_code")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }
    }

    public class BatchScrapeErrorsResponse
    {
        [JsonPropertyName("errors")]
        public List<BatchScrapeError> Errors { get; set; } = new();

        [JsonPropertyName("robots_blocked")]
        public List<string> RobotsBlocked { get; set; } = new();
    }
}
