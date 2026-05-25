using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Research.SearXng;

public class SearXngContentExtractor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SearXngContentExtractor> _logger;
    private readonly HashSet<string> _recentlyFailed = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastFailurePurge = DateTime.UtcNow;

    public SearXngContentExtractor(HttpClient httpClient, ILogger<SearXngContentExtractor> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Fetches a URL and extracts the main text content, similar to Tavily's content extraction.
    /// Uses SearXNG's own /search endpoint with the URL as query to get a cached snippet,
    /// then falls back to a lightweight HTML fetch + text extraction.
    /// </summary>
    public async Task<string?> ExtractAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        // Circuit breaker: skip domains that recently failed
        PurgeOldFailures();
        if (_recentlyFailed.Contains(url)) return null;

        try
        {
            // Strategy 1: Try fetching the page directly and extracting readable text
            var extracted = await FetchAndExtractAsync(url, ct);
            if (!string.IsNullOrWhiteSpace(extracted)) return extracted;

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Content extraction HTTP error for {Url}", url);
            _recentlyFailed.Add(url);
            return null;
        }
        catch (TaskCanceledException)
        {
            _recentlyFailed.Add(url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Content extraction failed for {Url}", url);
            return null;
        }
    }

    private async Task<string?> FetchAndExtractAsync(string url, CancellationToken ct)
    {
        // Use a short timeout since we don't want to block the pipeline
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(6));

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "ContentOS/1.0 (Research Bot; +https://contentos.ai)");
        request.Headers.Add("Accept", "text/html,application/xhtml+xml");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        if (!contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase) &&
            !contentType.Contains("text/plain", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        var html = await reader.ReadToEndAsync();

        return ExtractTextFromHtml(html);
    }

    private static string ExtractTextFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";

        // Remove script and style blocks entirely
        var text = html;

        // Remove <script> tags and content
        var scriptStart = 0;
        while ((scriptStart = text.IndexOf("<script", StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var scriptEnd = text.IndexOf("</script>", scriptStart, StringComparison.OrdinalIgnoreCase);
            if (scriptEnd < 0) scriptEnd = text.Length;
            text = text[..scriptStart] + text[(scriptEnd + 9)..];
        }

        // Remove <style> tags and content
        while ((scriptStart = text.IndexOf("<style", StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var styleEnd = text.IndexOf("</style>", scriptStart, StringComparison.OrdinalIgnoreCase);
            if (styleEnd < 0) styleEnd = text.Length;
            text = text[..scriptStart] + text[(styleEnd + 8)..];
        }

        // Remove <nav>, <footer>, <header>, <aside>, <form> blocks (boilerplate)
        foreach (var tag in new[] { "nav", "footer", "header", "aside", "form" })
        {
            var openTag = $"<{tag}";
            var closeTag = $"</{tag}>";
            while ((scriptStart = text.IndexOf(openTag, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var closePos = text.IndexOf(closeTag, scriptStart, StringComparison.OrdinalIgnoreCase);
                if (closePos < 0) closePos = text.Length;
                else closePos += closeTag.Length;
                text = text[..scriptStart] + text[closePos..];
            }
        }

        // Convert block elements to newlines
        foreach (var tag in new[] { "p", "div", "br", "h1", "h2", "h3", "h4", "h5", "h6", "li", "tr" })
        {
            text = text.Replace($"</{tag}>", "\n", StringComparison.OrdinalIgnoreCase);
            text = text.Replace($"<{tag}/>", "\n", StringComparison.OrdinalIgnoreCase);
            text = text.Replace($"<{tag} />", "\n", StringComparison.OrdinalIgnoreCase);
        }

        // Strip all remaining HTML tags
        var inTag = false;
        var result = new char[text.Length];
        var pos = 0;
        foreach (var c in text)
        {
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag) result[pos++] = c;
        }

        var plain = new string(result, 0, pos);

        // Decode common HTML entities
        plain = System.Net.WebUtility.HtmlDecode(plain);

        // Collapse whitespace and trim
        var lines = plain.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 20) // Skip short lines (nav items, etc.)
            .Take(15); // Take first ~15 meaningful lines

        var joined = string.Join(" ", lines);

        // Trim to reasonable length
        if (joined.Length > 600)
        {
            // Find a sentence boundary near 500 chars
            var cut = joined[..500].LastIndexOf('.');
            if (cut > 200) joined = joined[..(cut + 1)];
            else joined = joined[..500].TrimEnd() + "...";
        }

        return joined.Trim();
    }

    private void PurgeOldFailures()
    {
        if ((DateTime.UtcNow - _lastFailurePurge).TotalMinutes < 5) return;
        _recentlyFailed.Clear();
        _lastFailurePurge = DateTime.UtcNow;
    }
}