# A WebCrawler API .NET SDK

[![NuGet](https://img.shields.io/nuget/v/WebCrawlerApi.svg?style=flat-square)](https://www.nuget.org/packages/WebCrawlerApi)
[![NuGet](https://img.shields.io/nuget/dt/WebCrawlerApi.svg?style=flat-square)](https://www.nuget.org/packages/WebCrawlerApi)
[![License](https://img.shields.io/github/license/webcrawlerapi/webcrawlerapi-dotnet-sdk.svg?style=flat-square)](https://github.com/webcrawlerapi/webcrawlerapi-dotnet-sdk/blob/main/LICENSE)

A .NET SDK for interacting with the WebCrawlerAPI. WebCrawlerAPI allows you to turn any website into data. Read more at [WebCrawlerAPI](https://webcrawlerapi.com).

> In order to use the API you have to get an API key from [WebCrawlerAPI](https://dash.webcrawlerapi.com/access)

Read documentation at [WebCrawlerAPI Docs](https://webcrawlerapi.com/docs) for more information.

## Features

- ✅ **Complete Crawl API (v1)** - Multi-page web crawling with filters
- ✅ **Complete Scrape API (v2)** - Single-page scraping with AI extraction
- ✅ **Structured Outputs** - Extract data using JSON Schema (`response_schema`)
- ✅ **AI-Powered Extraction** - Use prompts to extract specific data
- ✅ **Async/Await Support** - Full async API with polling helpers
- ✅ **Content Lazy Loading** - Efficient content retrieval
- ✅ **No External Dependencies** - Uses only System.Net.Http and System.Text.Json
- ✅ **Comprehensive Test Suite** - 16 tests with 94% pass rate

## Requirements

- .NET 9.0 or higher

## Installation

Install the package via NuGet:

```bash
dotnet add package WebCrawlerApi
```

## Quick Start

### Single Page Scraping (v2/scrape)

```csharp
using WebCrawlerApi;
using WebCrawlerApi.Models;

// Initialize the client
var client = new WebCrawlerApiClient("YOUR_API_KEY");

// Basic scrape
var request = new ScrapeRequest
{
    Url = "https://example.com",
    OutputFormat = "markdown"
};

var result = await client.ScrapeAndWaitAsync(request);
Console.WriteLine(result.Markdown);
```

### Multi-Page Crawling (v1/crawl)

```csharp
var job = await client.CrawlAndWaitAsync(
    url: "https://example.com",
    scrapeType: "markdown",
    itemsLimit: 10
);

foreach (var item in job.JobItems)
{
    var content = await item.GetContentAsync();
    Console.WriteLine($"{item.Title}: {content?.Length} chars");
}
```

## Scrape API (v2/scrape)

The Scrape API is designed for single-page scraping with optional AI-powered extraction.

### Basic Scraping

```csharp
var request = new ScrapeRequest
{
    Url = "https://books.toscrape.com",
    OutputFormat = "markdown",  // "markdown", "cleaned", or "html"
    MaxAge = 0  // Bypass cache
};

var result = await client.ScrapeAndWaitAsync(request);
Console.WriteLine($"Page Title: {result.PageTitle}");
Console.WriteLine($"Status Code: {result.PageStatusCode}");
Console.WriteLine($"Content: {result.Markdown}");
```

### AI-Powered Extraction with Prompt

```csharp
var request = new ScrapeRequest
{
    Url = "https://books.toscrape.com",
    Prompt = "Extract the site name and main heading from the page"
};

var result = await client.ScrapeAndWaitAsync(request);
Console.WriteLine($"Structured Data: {JsonSerializer.Serialize(result.StructuredData)}");
```

### Structured Outputs with JSON Schema

```csharp
var schema = new
{
    type = "object",
    properties = new
    {
        site_name = new { type = "string" },
        main_heading = new { type = "string" },
        is_bookstore = new { type = "boolean" }
    },
    required = new[] { "site_name", "main_heading", "is_bookstore" },
    additionalProperties = false
};

var request = new ScrapeRequest
{
    Url = "https://books.toscrape.com",
    Prompt = "Extract site information according to the schema",
    ResponseSchema = schema
};

var result = await client.ScrapeAndWaitAsync(request);
var jsonElement = (JsonElement)result.StructuredData;

Console.WriteLine($"Site Name: {jsonElement.GetProperty("site_name")}");
Console.WriteLine($"Heading: {jsonElement.GetProperty("main_heading")}");
Console.WriteLine($"Is Bookstore: {jsonElement.GetProperty("is_bookstore")}");
```

### Async Scraping with Manual Polling

```csharp
// Start async scrape
var scrapeId = await client.ScrapeAsync(request);
Console.WriteLine($"Scrape started: {scrapeId.Id}");

// Poll for result
ScrapeResponse result;
do
{
    result = await client.GetScrapeAsync(scrapeId.Id);
    if (!result.Success && result.ErrorCode == null)
    {
        await Task.Delay(2000);
    }
} while (!result.Success && result.ErrorCode == null);

Console.WriteLine($"Scrape completed: {result.Success}");
```

## Crawl API (v1/crawl)

The Crawl API is designed for multi-page crawling operations.

### Synchronous Crawling

```csharp
var job = await client.CrawlAndWaitAsync(
    url: "https://example.com",
    scrapeType: "markdown",
    itemsLimit: 10,
    webhookUrl: "https://yourserver.com/webhook",
    allowSubdomains: false,
    maxPolls: 100  // Optional: maximum number of status checks
);

Console.WriteLine($"Job completed with status: {job.Status}");

// Access job items and their content
foreach (var item in job.JobItems)
{
    Console.WriteLine($"Page title: {item.Title}");
    Console.WriteLine($"Original URL: {item.OriginalUrl}");
    Console.WriteLine($"Item status: {item.Status}");

    // Get the content based on job's scrape_type
    var content = await item.GetContentAsync();
    if (content != null)
    {
        Console.WriteLine($"Content length: {content.Length}");
        Console.WriteLine($"Content preview: {content[..Math.Min(200, content.Length)]}...");
    }
}
```

### Asynchronous Crawling

```csharp
// Start crawl
var response = await client.CrawlAsync(
    url: "https://example.com",
    scrapeType: "markdown",
    itemsLimit: 10,
    webhookUrl: "https://yourserver.com/webhook",
    allowSubdomains: false
);

Console.WriteLine($"Crawling job started with ID: {response.Id}");

// Check job status
var job = await client.GetJobAsync(response.Id);
Console.WriteLine($"Job status: {job.Status}");
Console.WriteLine($"Number of items: {job.JobItems.Count}");

// Cancel a running job if needed
var message = await client.CancelJobAsync(response.Id);
Console.WriteLine($"Cancellation response: {message}");
```

### Crawling with Filters

```csharp
var job = await client.CrawlAndWaitAsync(
    url: "https://example.com",
    scrapeType: "markdown",
    itemsLimit: 20,
    whitelistRegexp: ".*/blog/.*",  // Only crawl blog posts
    blacklistRegexp: ".*/tag/.*",   // Skip tag pages
    mainContentOnly: true           // Extract main content only
);
```

## API Methods

### Scrape API Methods

#### `ScrapeAndWaitAsync(request, maxPolls = 100)`
Starts a scrape operation and waits for completion by polling the API.

#### `ScrapeAsync(request)`
Starts a scrape operation and returns immediately with a scrape ID.

#### `GetScrapeAsync(scrapeId)`
Retrieves the result of a scrape operation by ID.

### Crawl API Methods

#### `CrawlAndWaitAsync(...)`
Starts a new crawling job and waits for its completion. This method will continuously poll the job status until:
- The job reaches a terminal state (done, error, or cancelled)
- The maximum number of polls is reached (default: 100)
- The polling interval is determined by the server's `RecommendedPullDelayMs` or defaults to 5 seconds

#### `CrawlAsync(...)`
Starts a new crawling job and returns immediately with a job ID.

#### `GetJobAsync(jobId)`
Retrieves the current status and details of a specific job.

#### `CancelJobAsync(jobId)`
Cancels a running job.

## Parameters

### ScrapeRequest Properties

- `Url` (required): The URL to scrape
- `OutputFormat`: "markdown", "cleaned", or "html" (default: "markdown")
- `Prompt`: Natural language instruction for AI extraction
- `ResponseSchema`: JSON Schema object for structured outputs
- `WebhookUrl`: Webhook URL for completion notification
- `CleanSelectors`: CSS selectors to remove from the page
- `MainContentOnly`: Extract only main content (default: false)
- `RespectRobotsTxt`: Respect robots.txt (default: true)
- `MaxAge`: Maximum cache age in seconds (0 to bypass cache)
- `Actions`: Array of browser actions to perform

### ScrapeResponse Properties

- `Success`: Whether the scrape succeeded
- `Status`: "in_progress", "done", or "error"
- `Markdown`: Markdown content (if OutputFormat = "markdown")
- `CleanedContent`: Cleaned HTML (if OutputFormat = "cleaned")
- `RawContent`: Raw HTML (if OutputFormat = "html")
- `StructuredData`: Extracted structured data (if Prompt is provided)
- `Links`: Array of URLs found on the page
- `PageTitle`: The page title
- `PageStatusCode`: HTTP status code
- `ErrorCode`: Error code if failed
- `ErrorMessage`: Error message if failed

### Crawl Parameters

- `url` (required): The seed URL where the crawler starts
- `scrapeType`: "html", "cleaned", or "markdown" (default: "html")
- `itemsLimit`: Maximum number of pages to crawl (default: 10)
- `webhookUrl`: Webhook URL for completion notification
- `allowSubdomains`: Crawl subdomains (default: false)
- `whitelistRegexp`: Only crawl URLs matching this pattern
- `blacklistRegexp`: Skip URLs matching this pattern
- `mainContentOnly`: Extract only main content (default: false)
- `maxPolls`: Maximum polling attempts for CrawlAndWaitAsync (default: 100)

### JobItem Properties

- `Id`: Unique identifier
- `JobId`: Parent job identifier
- `OriginalUrl`: The page URL
- `PageStatusCode`: HTTP status code
- `Status`: "new", "in_progress", "done", "error"
- `Title`: Page title
- `Links`: Array of URLs found on the page
- `Cost`: Cost in credits
- `LastError`: Error message if failed
- `GetContentAsync()`: Retrieves page content (lazy-loaded)
- `RawContentUrl`: URL to raw content
- `CleanedContentUrl`: URL to cleaned content
- `MarkdownContentUrl`: URL to markdown content

## Testing

The SDK includes a comprehensive test suite with 16 tests covering all major functionality.

### Running Tests

```bash
cd sdk/dotnet/WebCrawlerApiTests
dotnet run
```

### Test Coverage

- ✅ 5 Scrape API tests (markdown, cleaned, html, prompt, structured output)
- ✅ 2 Async scrape tests (manual polling)
- ✅ 4 Crawl API tests (basic, filters, cleaned format, main content)
- ✅ 1 Async crawl test (manual polling)
- ✅ 2 Markdown content tests (access methods)
- ✅ 2 Job/Scrape retrieval tests

Expected output:
```
╔════════════════════════════════════════════════════════════╗
║    WebCrawlerAPI .NET SDK - Comprehensive Test Suite      ║
╚════════════════════════════════════════════════════════════╝

┌─ Scrape Tests ────────────────────────────────────────────
│ ✓ Test Scrape                                          5087ms
│ ✓ Test Scrape With Prompt                            17992ms
│ ✓ Test Scrape With Structured Output                 17952ms
│ ✓ Test Scrape Cleaned Format                          4994ms
│ ✓ Test Scrape Html Format                             4989ms
└────────────────────────────────────────────────────────────

...

╔════════════════════════════════════════════════════════════╗
║                      Test Summary                          ║
╠════════════════════════════════════════════════════════════╣
║  Total Tests:     16                                        ║
║  Passed:          15                   (94%)              ║
║  Failed:          1                                         ║
║  Duration:        105,85 seconds                            ║
╚════════════════════════════════════════════════════════════╝
```

## Examples

Check out the `WebCrawlerApiTests/Tests/` directory for comprehensive examples of all SDK features.

## License

MIT License
