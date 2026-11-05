# Batch scrape (.NET SDK)

Scrape a list of URLs in one async request. The API returns a batch `id` right away and scrapes the URLs in the background. You can wait with `ScrapeAndWaitAsync`, poll yourself with `GetBatchAsync`, or set `WebhookUrl` to be notified when the batch finishes.

See also: [guide](https://webcrawlerapi.com/docs/guides/batch-scrape) and [API reference](https://webcrawlerapi.com/docs/api/batch-scrape).

All methods live on `WebCrawlerApiClient` and use types from `WebCrawlerApi.Models`.

## Quick example

```csharp
using WebCrawlerApi;
using WebCrawlerApi.Models;

using var client = new WebCrawlerApiClient("YOUR_API_KEY");

var batch = await client.ScrapeAndWaitAsync(
    new[] { "https://example.com/", "https://example.com/pricing" },
    new BatchScrapeOptions { OutputFormats = new[] { "markdown" }, MaxConcurrency = 5 });

Console.WriteLine($"{batch.Status}: {batch.Completed}/{batch.Total} done, {batch.Failed} failed");
foreach (var item in batch.Data)
    Console.WriteLine($"{item.Url} {item.Status} {item.MarkdownUrl}");
```

## BatchScrapeOptions

Optional settings. Properties left `null` are not sent, so API defaults apply.

| Property | Type | Description |
| --- | --- | --- |
| `OutputFormats` | `string[]?` | `markdown`, `cleaned`, `html`, `links`, `images`. API default is `["markdown"]`. |
| `MainContentOnly` | `bool?` | Extract only the main content. API default `false`. |
| `CleanSelectors` | `string?` | CSS selectors to clean from the output. |
| `MaxConcurrency` | `int?` | Max URLs scraped in parallel for this batch. `0` or unset means no per-batch cap. |
| `IgnoreInvalidUrls` | `bool?` | Skip invalid URLs and report them in `InvalidUrls`. If `false`, any invalid URL fails the request. API default `true`. |
| `WebhookUrl` | `string?` | Public http(s) URL called once when the batch finishes. |
| `MaxAge` | `int?` | Cache duration in seconds. API default `604800` (7 days). `0` bypasses the cache. |
| `RespectRobotsTxt` | `bool?` | Respect each website's robots.txt. API default `false`. |

`prompt`, `response_schema` and `actions` are not supported in batch scrape.

## Methods

### ScrapeAndWaitAsync (batch overload)

```csharp
Task<BatchScrapeResponse> ScrapeAndWaitAsync(
    IEnumerable<string> urls,
    BatchScrapeOptions? options = null,
    int maxPolls = DefaultScrapeMaxPolls,
    CancellationToken cancellationToken = default)
```

Creates the batch, polls until it reaches a final status (`completed`, `failed`, `cancelled`), and returns progress plus the first page of items. `Next` is the cursor for more items.

Timeouts: polling stops after `maxPolls` polls or `ScrapeMaxWait`, whichever comes first. In that case the last known state is returned (not an exception), so check `IsTerminal` or `Status` (`scraping` means it is still running). It does not cancel the batch.

Errors: throws `HttpRequestException` if the create or status request fails (see below).

```csharp
var batch = await client.ScrapeAndWaitAsync(urls, new BatchScrapeOptions { MainContentOnly = true });
if (!batch.IsTerminal)
    Console.WriteLine($"Still running, id {batch.Id}");
```

### ScrapeBatchAsync

```csharp
Task<BatchScrapeCreated> ScrapeBatchAsync(
    IEnumerable<string> urls,
    BatchScrapeOptions? options = null,
    CancellationToken cancellationToken = default)
```

Creates a batch without waiting (`POST /v2/batch/scrape`). URLs are normalized and deduplicated by the API.

Returns `BatchScrapeCreated`:

- `Id` - batch id
- `StatusUrl` - URL of the status endpoint
- `InvalidUrls` - `List<BatchScrapeInvalidUrl>` (`Url`, `ErrorCode`, `ErrorMessage`) for rejected and duplicate URLs

Duplicates and rejected URLs appear in `InvalidUrls` (error codes such as `invalid_request`, `name_not_resolved`). A URL that was accepted but has the same page content as another page fails later as an item with `duplicate_item`; it shows up in `Data` and in the errors list, not in `InvalidUrls`.

```csharp
var created = await client.ScrapeBatchAsync(urls, new BatchScrapeOptions
{
    WebhookUrl = "https://your-server.com/webhook"
});
foreach (var bad in created.InvalidUrls)
    Console.WriteLine($"{bad.Url}: {bad.ErrorCode} {bad.ErrorMessage}");
```

### GetBatchAsync

```csharp
Task<BatchScrapeResponse> GetBatchAsync(
    string batchId,
    int? limit = null,
    string? cursor = null,
    CancellationToken cancellationToken = default)
```

Gets batch progress and one page of items (`GET /v2/batch/scrape/:id`). `limit` is items per page (1 to 500, API default 100). `cursor` is the `Next` value from the previous page.

`BatchScrapeResponse` fields: `Id`, `Status` (`scraping`, `completed`, `failed`, `cancelled`), `Total`, `Completed`, `Failed`, `Canceled`, `Data` (`List<BatchScrapeItem>`), `Next`, and the helper `IsTerminal`.

`BatchScrapeItem` fields: `Url`, `Status` (for example `done` or `error`), `PageStatusCode`, `Title`, `MarkdownUrl`, `CleanedUrl`, `HtmlUrl` (set for finished items and only for requested formats), `FromCache`, `ErrorCode`, `ErrorMessage` (set for failed items).

```csharp
var page = await client.GetBatchAsync(batchId, limit: 500);
```

### GetBatchErrorsAsync

```csharp
Task<BatchScrapeErrorsResponse> GetBatchErrorsAsync(string batchId, CancellationToken cancellationToken = default)
```

Lists failed URLs (`GET /v2/batch/scrape/:id/errors`). Returns `Errors` (`List<BatchScrapeError>` with `Url`, `ErrorCode`, `ErrorMessage`) and `RobotsBlocked` (`List<string>`, URLs blocked by robots.txt when `RespectRobotsTxt` is `true`). To retry failed URLs, submit them in a new batch.

```csharp
var errs = await client.GetBatchErrorsAsync(batchId);
foreach (var e in errs.Errors)
    Console.WriteLine($"{e.Url}: {e.ErrorCode}");
```

### CancelBatchAsync

```csharp
Task<string> CancelBatchAsync(string batchId, CancellationToken cancellationToken = default)
```

Cancels a running batch (`DELETE /v2/batch/scrape/:id`) and returns the resulting status (`"canceled"`). Items that did not start are marked canceled and are not charged. Cancelling a batch that already finished fails with a 400 error.

```csharp
var status = await client.CancelBatchAsync(batchId);
```

## Pagination

`Data` holds one page. While `Next` is not empty, fetch the next page:

```csharp
var all = new List<BatchScrapeItem>(batch.Data);
while (!string.IsNullOrEmpty(batch.Next))
{
    batch = await client.GetBatchAsync(batch.Id, limit: 500, cursor: batch.Next);
    all.AddRange(batch.Data);
}
```

## Webhook vs polling

- Webhook: set `WebhookUrl` and use `ScrapeBatchAsync`. The API sends one `POST` with the same body as the status endpoint (first 100 items) when the batch finishes. For larger batches fetch the remaining pages with `GetBatchAsync` and a cursor.
- Polling: use `ScrapeAndWaitAsync`, or call `GetBatchAsync` yourself until `IsTerminal`. Poll every few seconds, not faster.

## Errors

The batch methods throw `HttpRequestException` for any non-success HTTP response. The message is `"<request> failed: <API error message>"`, or `"<request> failed with status code <code>"` when the body has no message. API status codes:

- `400` - invalid parameters, no valid URLs, or batch already finished (on cancel)
- `401` - invalid or missing API key
- `402` - insufficient account balance
- `403` - organization suspended
- `404` - batch does not exist
- `500` - server-side error

Per-item errors (`ErrorCode`, `ErrorMessage`) are documented in [Errors](https://webcrawlerapi.com/docs/errors).
