using System.Text.Json;
using System.Text.RegularExpressions;
using ContentOS.Application.Research;

namespace ContentOS.Infrastructure.Research;

/// <summary>Preserves bounded collected evidence separately from inferred research notes.</summary>
/// <remarks>Cleaning is conservative, not an injection-proof semantic filter. Every excerpt remains untrusted data.</remarks>
public static class ResearchEvidenceHandoff
{
    /// <summary>Matches explicitly selected URLs only to existing collected findings; never manufactures support or semantic relevance.</summary>
    /// <param name="findings">Actual collected records.</param>
    /// <param name="selectedUrls">Untrusted model-selected URLs; absent or unknown values imply no support.</param>
    /// <returns>At most eight original findings with nonempty collected excerpts and valid selected provenance.</returns>
    public static List<ResearchFinding> SelectCollectedSources(IEnumerable<ResearchFinding> findings, IEnumerable<string>? selectedUrls)
    {
        var selected = (selectedUrls ?? []).Take(32).ToHashSet(StringComparer.Ordinal);
        return findings.Where(f => selected.Contains(f.SourceUrl) && !string.IsNullOrWhiteSpace(CleanExcerpt(f.SourceExcerpt))
            && Uri.TryCreate(f.SourceUrl, UriKind.Absolute, out var uri) && (uri.Scheme is "http" or "https"))
            .DistinctBy(f => f.SourceUrl).Take(8).ToList();
    }

    /// <summary>Bounds complete evidence fields before JSON encoding, preserving valid envelopes and mandatory limitations.</summary>
    /// <param name="summary">Existing evidence envelope or legacy plain-text summary.</param>
    /// <returns>A complete bounded envelope, or bounded legacy text that remains untrusted.</returns>
    public static string BoundWriterSummary(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary)) return string.Empty;
        if (summary.Length <= 40000)
        {
            try
            {
                using var data = JsonDocument.Parse(summary);
                if (data.RootElement.ValueKind == JsonValueKind.Object && data.RootElement.TryGetProperty("excerpt", out _))
                {
                    string Field(string key) => data.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
                    return ToWriterSummary(new ResearchFinding { SourceType = Field("sourceType"), SourceTitle = Field("title"),
                        SourceUrl = Field("url"), SourceExcerpt = Field("excerpt"), Notes = Field("inferredNotes") });
                }
            }
            catch (JsonException) { /* Old plain-text snapshots are supported as untrusted text. */ }
        }
        return Bound(summary, 6000);
    }
    /// <summary>Removes obvious HTML/UI debris and known instruction-like fragments while retaining budget numbers.</summary>
    /// <param name="content">Collected search content, possibly a snippet rather than the full page.</param>
    /// <returns>At most 1600 characters of normalized untrusted evidence, or empty if no usable text remains.</returns>
    public static string CleanExcerpt(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;
        var text = content[..Math.Min(content.Length, 32000)];
        text = Regex.Replace(text, @"<(script|style|nav|header|footer|aside|form|button|svg)\b[^>]*>.*?(</\1>|$)",
            " ", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
        text = Regex.Replace(text, @"</?(p|div|li|br|h[1-6])\b[^>]*>", "\n", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        text = Regex.Replace(text, "<[^>]+>", " ", RegexOptions.None, TimeSpan.FromSeconds(1));
        text = System.Net.WebUtility.HtmlDecode(text);
        var fragments = text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !Regex.IsMatch(line, @"ignore.{0,40}(instructions|rules)|system prompt|run.{0,15}shell|read.{0,30}(credentials|secrets)|api[_ ]key",
                RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)));
        text = string.Join(" ", fragments);
        text = Regex.Replace(text, @"menu list icon|trend unchanged icon|arrow up icon|table of contents", " ", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        text = Regex.Replace(text, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();
        return text[..Math.Min(text.Length, 1600)];
    }

    /// <summary>Encodes an evidence envelope as a string for the existing List-of-string writer contract.</summary>
    /// <param name="finding">Collected excerpt plus provenance and explicitly inferred notes.</param>
    /// <returns>Bounded JSON data with limitations; it never promotes a title or inferred note to source evidence.</returns>
    public static string ToWriterSummary(ResearchFinding finding)
    {
        var excerpt = CleanExcerpt(finding.SourceExcerpt);
        var validUrl = Uri.TryCreate(finding.SourceUrl, UriKind.Absolute, out var url) && (url.Scheme is "http" or "https")
            && finding.SourceUrl.Length <= 2000;
        var limits = new List<string> { "Retrieved content may be a search snippet; full-page completeness and factual accuracy are unverified." };
        if (!validUrl) limits.Add("Source URL is unavailable or invalid; do not fabricate a citation.");
        if (string.IsNullOrWhiteSpace(excerpt)) limits.Add("No usable collected excerpt; title and inferred notes do not support factual claims.");
        return JsonSerializer.Serialize(new { sourceType = Bound(finding.SourceType, 100), title = Bound(finding.SourceTitle, 300),
            url = validUrl ? finding.SourceUrl : string.Empty, excerpt, inferredNotes = Bound(finding.Notes, 800),
            untrustedSourceData = true, limitations = limits });
    }

    /// <summary>Matches a valid HTTP(S) source at an exact domain or subdomain boundary.</summary>
    /// <param name="url">Source URL.</param><param name="domain">Requested domain.</param>
    /// <returns>False for malformed URLs, unsupported schemes, and lookalike domains.</returns>
    public static bool MatchesDomain(string url, string domain) => Uri.TryCreate(url,UriKind.Absolute,out var uri)
        && uri.Scheme is "http" or "https" && (uri.Host.Equals(domain,StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("."+domain,StringComparison.OrdinalIgnoreCase));

    /// <summary>Retains observed wording without inventing a missing audience question.</summary>
    /// <param name="title">Collected source title.</param><param name="excerpt">Cleaned source text.</param>
    /// <returns>A collected question when present, otherwise the source title or excerpt.</returns>
    public static string ObservedQuestion(string title, string excerpt)
    {
        var cleanedTitle=CleanExcerpt(title);
        if(cleanedTitle.Contains('?')) return cleanedTitle;
        var question=Regex.Match(excerpt,@"(?:^|[.!]\s+)([^.!?]*\?)",RegexOptions.None,TimeSpan.FromSeconds(1));
        return question.Success?question.Groups[1].Value.Trim():(!string.IsNullOrWhiteSpace(cleanedTitle)?cleanedTitle:excerpt);
    }

    /// <summary>Balances usable original records round-robin by provider with URL deduplication.</summary>
    /// <param name="findings">Collected source records.</param><param name="limit">Prompt evidence budget.</param>
    /// <returns>At most the budget, excluding invalid URLs and empty excerpts.</returns>
    public static List<ResearchFinding> BalanceCollectedSources(IEnumerable<ResearchFinding> findings, int limit=8)
    {
        var groups=findings.Where(f=>!string.IsNullOrWhiteSpace(CleanExcerpt(f.SourceExcerpt)) && Uri.TryCreate(f.SourceUrl,UriKind.Absolute,out var uri) && uri.Scheme is "http" or "https")
            .GroupBy(f=>string.IsNullOrWhiteSpace(f.ProviderName)?f.SourceType:f.ProviderName).Select(g=>new Queue<ResearchFinding>(g)).ToList();
        var selected=new List<ResearchFinding>(); var seen=new HashSet<string>(StringComparer.Ordinal);
        while(selected.Count<limit && groups.Any(g=>g.Count>0))
            foreach(var group in groups)
            {
                while(group.Count>0){var finding=group.Dequeue();if(seen.Add(finding.SourceUrl)){selected.Add(finding);break;}}
                if(selected.Count>=limit) break;
            }
        return selected;
    }
    private static string Bound(string? text, int length) => string.IsNullOrWhiteSpace(text) ? string.Empty : text[..Math.Min(text.Length, length)];
}
