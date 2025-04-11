using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WebCrawlerApi.Models
{
    public class Job
    {
        private static readonly HashSet<string> TerminalStatuses = new() { "done", "error", "cancelled" };

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("org_id")]
        public string OrgId { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("scrape_type")]
        public string ScrapeType { get; set; } = string.Empty;

        [JsonPropertyName("whitelist_regexp")]
        public string WhitelistRegexp { get; set; } = string.Empty;

        [JsonPropertyName("blacklist_regexp")]
        public string BlacklistRegexp { get; set; } = string.Empty;

        [JsonPropertyName("allow_subdomains")]
        public bool AllowSubdomains { get; set; }

        [JsonPropertyName("items_limit")]
        public int ItemsLimit { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("webhook_url")]
        public string WebhookUrl { get; set; } = string.Empty;

        [JsonPropertyName("recommended_pull_delay_ms")]
        public int RecommendedPullDelayMs { get; set; }

        [JsonPropertyName("finished_at")]
        public DateTime? FinishedAt { get; set; }

        [JsonPropertyName("webhook_status")]
        public string? WebhookStatus { get; set; }

        [JsonPropertyName("webhook_error")]
        public string? WebhookError { get; set; }

        [JsonPropertyName("error_code")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("job_items")]
        public IReadOnlyList<JobItem> JobItems { get; set; } = new List<JobItem>();

        public bool IsTerminal => TerminalStatuses.Contains(Status);
    }
} 