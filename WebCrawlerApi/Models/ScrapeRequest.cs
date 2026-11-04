using System.Text.Json.Serialization;

namespace WebCrawlerApi.Models
{
    public class ScrapeRequest
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("output_format")]
        public string? OutputFormat { get; set; }

        [JsonPropertyName("webhook_url")]
        public string? WebhookUrl { get; set; }

        [JsonPropertyName("clean_selectors")]
        public string? CleanSelectors { get; set; }

        [JsonPropertyName("actions")]
        public object[]? Actions { get; set; }

        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; }

        [JsonPropertyName("response_schema")]
        public object? ResponseSchema { get; set; }

        [JsonPropertyName("respect_robots_txt")]
        public bool? RespectRobotsTxt { get; set; }

        [JsonPropertyName("main_content_only")]
        public bool? MainContentOnly { get; set; }

        [JsonPropertyName("max_age")]
        public int? MaxAge { get; set; }
    }
}
