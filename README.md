# A WebCrawler API .NET SDK

[![NuGet](https://img.shields.io/nuget/v/WebCrawlerApi.svg?style=flat-square)](https://www.nuget.org/packages/WebCrawlerApi)
[![NuGet](https://img.shields.io/nuget/dt/WebCrawlerApi.svg?style=flat-square)](https://www.nuget.org/packages/WebCrawlerApi)
[![License](https://img.shields.io/github/license/webcrawlerapi/webcrawlerapi-dotnet-sdk.svg?style=flat-square)](https://github.com/webcrawlerapi/webcrawlerapi-dotnet-sdk/blob/main/LICENSE)

A .NET SDK for interacting with the WebCrawlerAPI. WebCrawlerAPI allows you to turn any website into data. Read more at [WebCrawlerAPI](https://webcrawlerapi.com).

> In order to use the API you have to get an API key from [WebCrawlerAPI](https://dash.webcrawlerapi.com/access)

Read documentation at [WebCrawlerAPI Docs](https://webcrawlerapi.com/docs) for more information.

## Requirements

- .NET 7.0 or higher

## Installation

Install the package via NuGet:

```bash
dotnet add package WebCrawlerApi
```

## Usage

```csharp
using WebCrawlerApi;
using WebCrawlerApi.Models;

// Initialize the client
var crawler = new WebCrawlerApiClient("YOUR_API_KEY");

// Synchronous crawling (blocks until completion)
var job = await crawler.CrawlAndWaitAsync(
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
    // Returns null if item is not in "done" status
    var content = await item.GetContentAsync();
    if (content != null)
    {
        Console.WriteLine($"Content length: {content.Length}");
        Console.WriteLine($"Content preview: {content[..Math.Min(200, content.Length)]}...");
    }
    else
    {
        Console.WriteLine("Content not available or item not done");
    }
}

// Or use asynchronous crawling
var response = await crawler.CrawlAsync(
    url: "https://example.com",
    scrapeType: "markdown",
    itemsLimit: 10,
    webhookUrl: "https://yourserver.com/webhook",
    allowSubdomains: false
);

// Get the job ID from the response
var jobId = response.Id;
Console.WriteLine($"Crawling job started with ID: {jobId}");

// Check job status and get results
var job = await crawler.GetJobAsync(jobId);
Console.WriteLine($"Job status: {job.Status}");

// Access job details
Console.WriteLine($"Crawled URL: {job.Url}");
Console.WriteLine($"Created at: {job.CreatedAt:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine($"Number of items: {job.JobItems.Count}");

// Cancel a running job if needed
var message = await crawler.CancelJobAsync(jobId);
Console.WriteLine($"Cancellation response: {message}");
```

## API Methods

### CrawlAndWaitAsync()
Starts a new crawling job and waits for its completion. This method will continuously poll the job status until:
- The job reaches a terminal state (done, error, or cancelled)
- The maximum number of polls is reached (default: 100)
- The polling interval is determined by the server's `RecommendedPullDelayMs` or defaults to 5 seconds

### CrawlAsync()
Starts a new crawling job and returns immediately with a job ID. Use this when you want to handle polling and status checks yourself, or when using webhooks.

### GetJobAsync()
Retrieves the current status and details of a specific job.

### CancelJobAsync()
Cancels a running job. Any items that are not in progress or already completed will be marked as canceled and will not be charged.

## Parameters

### Crawl Methods (CrawlAndWaitAsync and CrawlAsync)
- `url` (required): The seed URL where the crawler starts. Can be any valid URL.
- `scrapeType` (default: "html"): The type of scraping you want to perform. Can be "html", "cleaned", or "markdown".
- `itemsLimit` (default: 10): Crawler will stop when it reaches this limit of pages for this job.
- `webhookUrl` (optional): The URL where the server will send a POST request once the task is completed.
- `allowSubdomains` (default: false): If true, the crawler will also crawl subdomains.
- `whitelistRegexp` (optional): A regular expression to whitelist URLs. Only URLs that match the pattern will be crawled.
- `blacklistRegexp` (optional): A regular expression to blacklist URLs. URLs that match the pattern will be skipped.
- `maxPolls` (optional, CrawlAndWaitAsync only): Maximum number of status checks before returning (default: 100)

### JobItem Properties

Each JobItem object represents a crawled page and contains:

- `Id`: The unique identifier of the item
- `JobId`: The parent job identifier
- `OriginalUrl`: The URL of the page
- `PageStatusCode`: The HTTP status code of the page request
- `Status`: The status of the item (new, in_progress, done, error)
- `Title`: The page title
- `CreatedAt`: The date when the item was created
- `Cost`: The cost of the item in $
- `ReferredUrl`: The URL where the page was referred from
- `LastError`: Any error message if the item failed
- `ErrorCode`: The error code associated with the job if it failed
- `GetContentAsync()`: Method to get the page content based on the job's ScrapeType (html, cleaned, or markdown). Returns null if the item's status is not "done" or if content is not available. Content is automatically fetched and cached when accessed.
- `RawContentUrl`: URL to the raw content (if available)
- `CleanedContentUrl`: URL to the cleaned content (if ScrapeType is "cleaned")
- `MarkdownContentUrl`: URL to the markdown content (if ScrapeType is "markdown")

## License

MIT License 