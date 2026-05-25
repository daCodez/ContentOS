namespace ContentOS.Application.Research;

public enum TopicType
{
    Problem = 0,
    Solution = 1,
    Comparison = 2,
    Outcome = 3,
    Scenario = 4,
    Authority = 5,
    CTR = 6
}

public static class TopicTypeLabels
{
    public static readonly Dictionary<TopicType, string> Labels = new()
    {
        { TopicType.Problem, "Problem" },
        { TopicType.Solution, "Solution" },
        { TopicType.Comparison, "Comparison" },
        { TopicType.Outcome, "Outcome" },
        { TopicType.Scenario, "Scenario" },
        { TopicType.Authority, "Authority" },
        { TopicType.CTR, "CTR" }
    };

    public static string ToLabel(this TopicType type) => Labels.GetValueOrDefault(type, "Unknown");

    public static TopicType? FromLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        return Labels.FirstOrDefault(kvp =>
            string.Equals(kvp.Value, label, StringComparison.OrdinalIgnoreCase)).Key;
    }
}

public class TopicDiversityConfig
{
    public int MinPerType { get; set; } = 1;
    public int MaxPerPattern { get; set; } = 1;
    public int MinScoreToKeep { get; set; } = 7;
    public int ClusterSize { get; set; } = 7;
    public int MaxAuthorityDomainsInSerp { get; set; } = 3;
    public List<string> BlockedPatterns { get; set; } = new()
    {
        "what beginners get wrong",
        "what beginners get stuck on",
        "what beginners keep getting",
        "beginners keep getting wrong",
        "beginners keep getting stuck"
    };

    /// <summary>
    /// Domains that signal high-competition SERPs — if too many appear in top results,
    /// the keyword is dominated and hard for a new site to rank.
    /// </summary>
    public HashSet<string> AuthorityDomains { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "nerdwallet.com", "investopedia.com", "bankrate.com", "creditkarma.com",
        "fool.com", "forbes.com", "cnbc.com", "bloomberg.com",
        "businessinsider.com", "money.com", "thebalance.com", "wisebread.com",
        "nerdwallet.org", "finance.yahoo.com", "usatoday.com", "marketwatch.com",
        "wikipedia.org", "wikihow.com"
    };

    /// <summary>
    /// Title/angle signals that correlate with low-competition keywords.
    /// Topics with these get a boost.
    /// </summary>
    public List<string> LowCompetitionSignals { get; set; } = new()
    {
        "for beginners", "simple", "step by step", "step-by-step",
        "on $", "with bad credit", "for low income", "without",
        "for single", "with irregular", "from scratch", "no experience",
        "for dummies", "made easy", "quick start", "basics"
    };

    /// <summary>
    /// Topic angles that make natural monetization difficult.
    /// If a topic matches these and has no monetization path, it should be flagged.
    /// </summary>
    public List<string> HardToMonetizeSignals { get; set; } = new()
    {
        "definition of", "what is", "history of", "meaning of",
        "who invented", "etymology", "philosophy of"
    };
}