namespace ContentOS.Infrastructure.Research.Abstractions;

public class SearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public decimal Score { get; set; }

    public string Domain
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Url)) return string.Empty;
            try
            {
                var host = new Uri(Url).Host.ToLowerInvariant();
                return host.StartsWith("www.") ? host[4..] : host;
            }
            catch { return string.Empty; }
        }
    }
}