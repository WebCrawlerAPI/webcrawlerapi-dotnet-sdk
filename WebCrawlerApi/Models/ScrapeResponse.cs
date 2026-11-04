using System.Text.Json.Serialization;

namespace WebCrawlerApi.Models
{
    public class ScrapeResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("markdown")]
        public string? Markdown { get; set; }

        [JsonPropertyName("cleaned_content")]
        public string? CleanedContent { get; set; }

        [JsonPropertyName("raw_content")]
        public string? RawContent { get; set; }

        [JsonPropertyName("page_status_code")]
        public int PageStatusCode { get; set; }

        [JsonPropertyName("page_title")]
        public string? PageTitle { get; set; }

        [JsonPropertyName("structured_data")]
        public object? StructuredData { get; set; }

        [JsonPropertyName("links")]
        public string[]? Links { get; set; }

        [JsonPropertyName("error_code")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("recommended_pull_delay_ms")]
        public int RecommendedPullDelayMs { get; set; }

        public bool IsInProgress => Status != "done" && Status != "error" && !Success;
    }
}
