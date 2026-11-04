using System.Text.Json.Serialization;

namespace WebCrawlerApi.Models
{
    public class ScrapeId
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }
}
