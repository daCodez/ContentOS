using ContentOS.Application.Research;
using ContentOS.Domain.Entities;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ContentOS.Infrastructure.Research;

public interface IIdeaDeduplicator
{
    bool IsDuplicate(CandidateContentIdea candidate, IEnumerable<ContentIdea> existingIdeas);
    bool IsDuplicate(CandidateContentIdea candidate, IEnumerable<CandidateContentIdea> existingCandidates);
    bool IsTooSimilarToSource(string candidateTitle, IEnumerable<string> sourceTitles, double threshold = 0.7);
    string ExtractCanonicalTopic(string title, string keyword);
    string ExtractAngle(string title, string keyword, string recommendedAngle);
    string ExtractIntent(string searchIntent);
    string ExtractPainPoint(string audiencePainPoint);
    string NormalizeForComparison(string input);
    string CleanTitleAndKeyword(string input);
}

public class IdeaDeduplicatorService : IIdeaDeduplicator
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "been", "by", "for", "from", "has", "have", "he", "in", "is", "it", "its", "of", "on", "that", "the", "to", "was", "were", "will", "with"
    };

    private static readonly HashSet<string> BannedPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "reddit", "youtube", "quora", "facebook", "twitter", "instagram", "tiktok", "pinterest", "linkedin", "google", "bing", "amazon", "ebay", "walmart", "target", "site", "forum", "blog"
    };

    public bool IsDuplicate(CandidateContentIdea candidate, IEnumerable<ContentIdea> existingIdeas)
    {
        if (existingIdeas == null || !existingIdeas.Any()) return false;

        var candidateTopic = ExtractCanonicalTopic(candidate.Title, candidate.PrimaryKeyword);
        var candidateAngle = ExtractAngle(candidate.Title, candidate.PrimaryKeyword, candidate.RecommendedAngle);
        var candidateIntent = ExtractIntent(candidate.SearchIntent);
        var candidatePain = ExtractPainPoint(candidate.AudiencePainPoint);

        foreach (var existing in existingIdeas)
        {
            // Use stored metadata if available, otherwise fall back to re-computing
            var existingTopic = !string.IsNullOrWhiteSpace(existing.CanonicalTopic)
                ? existing.CanonicalTopic
                : ExtractCanonicalTopic(existing.Title, existing.PrimaryKeyword);

            var existingAngle = !string.IsNullOrWhiteSpace(existing.Angle)
                ? existing.Angle
                : ExtractAngle(existing.Title, existing.PrimaryKeyword, existing.RecommendedAngle);

            var existingIntent = !string.IsNullOrWhiteSpace(existing.Intent)
                ? existing.Intent
                : ExtractIntent(existing.SearchIntent);

            var existingPain = !string.IsNullOrWhiteSpace(existing.PainPoint)
                ? existing.PainPoint
                : ExtractPainPoint(existing.AudiencePainPoint);

            // --- DEDUPLICATION RULE ---
            // Reject if ALL FOUR components are the same or very close
            var topicMatch = candidateTopic == existingTopic;
            var angleMatch = candidateAngle == existingAngle;
            var intentMatch = candidateIntent == existingIntent;
            var painMatch = candidatePain == existingPain;

            if (topicMatch && angleMatch && intentMatch && painMatch)
            {
                return true; // Duplicate: same topic, same angle, same intent, same pain point
            }

            // ALSO reject if the title is a near-duplicate (minor wording variation)
            var cleanCandidateTitle = CleanTitleAndKeyword(candidate.Title);
            var cleanExistingTitle = CleanTitleAndKeyword(existing.Title);
            if (!string.IsNullOrWhiteSpace(cleanCandidateTitle) && !string.IsNullOrWhiteSpace(cleanExistingTitle))
            {
                var titleSimilarity = CalculateSimilarity(cleanCandidateTitle, cleanExistingTitle);
                if (titleSimilarity > 0.85) // Very high similarity in title wording
                {
                    return true; // Duplicate: nearly identical title
                }
            }
        }

        return false; // Not a duplicate
    }

    public bool IsDuplicate(CandidateContentIdea candidate, IEnumerable<CandidateContentIdea> existingCandidates)
    {
        if (existingCandidates == null || !existingCandidates.Any()) return false;

        var candidateTopic = ExtractCanonicalTopic(candidate.Title, candidate.PrimaryKeyword);
        var candidateAngle = ExtractAngle(candidate.Title, candidate.PrimaryKeyword, candidate.RecommendedAngle);
        var candidateIntent = ExtractIntent(candidate.SearchIntent);
        var candidatePain = ExtractPainPoint(candidate.AudiencePainPoint);

        foreach (var existing in existingCandidates)
        {
            var existingTopic = ExtractCanonicalTopic(existing.Title, existing.PrimaryKeyword);
            var existingAngle = ExtractAngle(existing.Title, existing.PrimaryKeyword, existing.RecommendedAngle);
            var existingIntent = ExtractIntent(existing.SearchIntent);
            var existingPain = ExtractPainPoint(existing.AudiencePainPoint);

            var topicMatch = candidateTopic == existingTopic;
            var angleMatch = candidateAngle == existingAngle;
            var intentMatch = candidateIntent == existingIntent;
            var painMatch = candidatePain == existingPain;

            if (topicMatch && angleMatch && intentMatch && painMatch)
                return true;

            var cleanCandidateTitle = CleanTitleAndKeyword(candidate.Title);
            var cleanExistingTitle = CleanTitleAndKeyword(existing.Title);
            if (!string.IsNullOrWhiteSpace(cleanCandidateTitle) && !string.IsNullOrWhiteSpace(cleanExistingTitle))
            {
                var titleSimilarity = CalculateSimilarity(cleanCandidateTitle, cleanExistingTitle);
                if (titleSimilarity > 0.85)
                    return true;
            }
        }

        return false;
    }

    private double CalculateSimilarity(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0.0;
        var normA = NormalizeForComparison(a);
        var normB = NormalizeForComparison(b);
        var setA = new HashSet<string>(normA.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var setB = new HashSet<string>(normB.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var intersection = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();
        return union == 0 ? 0.0 : (double)intersection / union;
    }

    public string NormalizeForComparison(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var lowered = input.ToLowerInvariant();
        var noPunct = Regex.Replace(lowered, @"\p{P}", " ");
        var words = noPunct.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var filtered = words.Where(w => !StopWords.Contains(w) && w.Length > 1);
        return string.Join(" ", filtered);
    }

    public string CleanTitleAndKeyword(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var cleaned = input.Trim();
        // Remove trailing " - Source" or " | SiteName"
        cleaned = Regex.Replace(cleaned, @"\s*[-|]\s*\w+$", "");
        // Remove trailing "(Source)"
        cleaned = Regex.Replace(cleaned, @"\s*\([^)]*\)$", "");
        // Remove trailing URL
        cleaned = Regex.Replace(cleaned, @"https?://[^\s]+$", "");
        return cleaned.TrimEnd('-', '|', '(', ' ', ')');
    }

    public string ExtractCanonicalTopic(string title, string keyword)
    {
        // Start with the cleaned title and keyword
        var combined = $"{title} {keyword}".Trim();

        // Remove angle indicators to get the core topic
        var cleaned = combined;
        cleaned = Regex.Replace(cleaned, @"\b(how to|ways to|steps to|guide to|tips for|mistakes in|myth about|fact vs|debunking|truth about|secret of|what nobody tells you|i tried|stop|avoid|never)\b", " ", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\b(vs|versus|compared to|better than|alternative to)\b", " ", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\b(save|pay off|earn|make|lose|gain)\b", " ", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\b\d+\s*(dollars?|dollars|\$|%|percent)\b", " ", RegexOptions.IgnoreCase); // Remove amounts
        cleaned = Regex.Replace(cleaned, @"\b(first|second|third|7 days|30 days|week|month|year)s?\b", " ", RegexOptions.IgnoreCase); // Remove timeframes
        cleaned = Regex.Replace(cleaned, @"\b(for beginners|for dummies|step by step|simple|easy|quick|fast)\b", " ", RegexOptions.IgnoreCase);

        // Normalize and remove stopwords
        var normalized = NormalizeForComparison(cleaned);
        return normalized;
    }

    public string ExtractAngle(string title, string keyword, string recommendedAngle)
    {
        // Start with the provided angle, fall back to deriving from title/keyword
        var angle = !string.IsNullOrWhiteSpace(recommendedAngle) ? recommendedAngle.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(angle))
        {
            var combined = $"{title} {keyword}".ToLowerInvariant();
            if (combined.Contains("how to") || combined.Contains("ways to") || combined.Contains("steps to")) angle = "how-to";
            else if (combined.Contains("mistake") || combined.Contains("error") || combined.Contains("fail") || combined.Contains("wrong") || combined.Contains("avoid")) angle = "mistakes";
            else if (combined.StartsWith("myth") || combined.Contains("fact vs myth") || combined.Contains("debunking")) angle = "myth";
            else if (combined.Contains("scared") || combined.Contains("afraid") || combined.Contains("overwhelmed") || combined.Contains("intimidated") || combined.Contains("don't know where to start")) angle = "beginner-fear";
            else if (combined.Contains("fast") || combined.Contains("quick") || combined.Contains("today") || combined.Contains("immediately") || combined.Contains("in 7 days")) angle = "quick-win";
            else if (combined.Contains("vs") || combined.Contains("versus") || combined.Contains("better than") || combined.Contains("alternative to")) angle = "comparison";
            else if (combined.Contains("stress") || combined.Contains("worry") || combined.Contains("anxiety") || combined.Contains("guilt") || combined.Contains("shame") || combined.Contains("frustrated") || combined.Contains("hopeless")) angle = "emotional-trigger";
            else angle = "general";
        }
        return angle.ToLowerInvariant();
    }

    public string ExtractIntent(string searchIntent)
    {
        if (string.IsNullOrWhiteSpace(searchIntent)) return "informational";
        return searchIntent.Trim().ToLowerInvariant();
    }

    public string ExtractPainPoint(string audiencePainPoint)
    {
        if (string.IsNullOrWhiteSpace(audiencePainPoint)) return string.Empty;
        return NormalizeForComparison(audiencePainPoint);
    }

    public bool IsTooSimilarToSource(string candidateTitle, IEnumerable<string> sourceTitles, double threshold = 0.7)
    {
        if (string.IsNullOrWhiteSpace(candidateTitle) || sourceTitles == null || !sourceTitles.Any())
            return false;

        var cleanCandidate = CleanTitleAndKeyword(candidateTitle);
        var candidateNorm = NormalizeForComparison(cleanCandidate);

        foreach (var sourceTitle in sourceTitles)
        {
            if (string.IsNullOrWhiteSpace(sourceTitle)) continue;
            var cleanSource = CleanTitleAndKeyword(sourceTitle);
            var sourceNorm = NormalizeForComparison(cleanSource);

            var similarity = CalculateSimilarity(candidateNorm, sourceNorm);
            if (similarity > threshold)
                return true;
        }

        return false;
    }
}