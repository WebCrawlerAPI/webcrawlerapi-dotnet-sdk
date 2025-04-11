using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace WebCrawlerApi.Models
{
    public class JobItem
    {
        private readonly Job _job;
        private readonly HttpClient _httpClient;
        private string? _content;

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("job_id")]
        public string JobId { get; set; } = string.Empty;

        [JsonPropertyName("original_url")]
        public string OriginalUrl { get; set; } = string.Empty;

        [JsonPropertyName("page_status_code")]
        public int PageStatusCode { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("cost")]
        public float Cost { get; set; }

        [JsonPropertyName("referred_url")]
        public string ReferredUrl { get; set; } = string.Empty;

        [JsonPropertyName("last_error")]
        public string LastError { get; set; } = string.Empty;

        [JsonPropertyName("raw_content_url")]
        public string? RawContentUrl { get; set; }

        [JsonPropertyName("cleaned_content_url")]
        public string? CleanedContentUrl { get; set; }

        [JsonPropertyName("markdown_content_url")]
        public string? MarkdownContentUrl { get; set; }

        [JsonConstructor]
        public JobItem()
        {
            // This constructor is used by the JSON deserializer
            _job = null!;
            _httpClient = null!;
        }

        internal JobItem(Job job, HttpClient httpClient)
        {
            _job = job;
            _httpClient = httpClient;
        }

        public async Task<string?> GetContentAsync()
        {
            if (Status != "done")
                return null;

            if (_content != null)
                return _content;

            var contentUrl = _job.ScrapeType switch
            {
                "html" => RawContentUrl,
                "cleaned" => CleanedContentUrl,
                "markdown" => MarkdownContentUrl,
                _ => null
            };

            if (string.IsNullOrEmpty(contentUrl))
                return null;

            var response = await _httpClient.GetStringAsync(contentUrl);
            _content = response;
            return _content;
        }
    }
} 