using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ContentOS.Infrastructure.Workflow;

/// <summary>Produces readable local diagnostics from allowlisted metadata, never model or source content.</summary>
public static class WorkflowDiagnostics
{
    /// <summary>Identifies a configuration without exposing host names, credentials, paths or URL queries.</summary>
    /// <param name="endpoint">Configured provider endpoint.</param>
    /// <returns>A stable hash of scheme, host and port only.</returns>
    public static string ConfigurationId(Uri? endpoint) => endpoint is null ? "unknown"
        : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{endpoint.Scheme}|{endpoint.IdnHost}|{endpoint.Port}")))[..12];

    /// <summary>Hashes a source reference without emitting its path, query, credentials or fragment.</summary>
    public static string SourceId(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var parsed)
        ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{parsed.Scheme}|{parsed.IdnHost}|{parsed.Port}|{parsed.AbsolutePath}")))[..12] : "unknown";

    /// <summary>Logs safe exception identity and next action without a content-bearing exception object.</summary>
    public static void LogFailure(ILogger logger, Exception exception, string explanation, string sourceId = "unknown")
    {
        var failure = Failure(exception);
        logger.LogWarning("{Explanation} {NextAction} SourceId={SourceId} FailureCode={FailureCode} ExceptionType={ExceptionType} ExceptionCode={ExceptionCode} StackMethods={StackMethods}", explanation, failure.Summary, sourceId, failure.Code, failure.ExceptionType, failure.ExceptionCode, failure.StackMethods);
    }

    /// <summary>Allows a bounded model/capability identifier rather than an arbitrary text or URL value.</summary>
    /// <param name="value">A configured technical identifier.</param>
    /// <returns>The identifier or an explicit unknown marker.</returns>
    public static string Identifier(string? value) => value is not null && !value.Contains("://", StringComparison.Ordinal)
        && Regex.IsMatch(value, @"\A[a-zA-Z0-9_:./-]{1,100}\z", RegexOptions.None, TimeSpan.FromSeconds(1)) ? value : "unknown";

    /// <summary>Maps validation text to fixed rule names without copying article wording into diagnostics.</summary>
    /// <param name="message">Internal failure text; never returned directly.</param>
    /// <returns>Known actionable rule names; no quoted keyword, URL, prompt or draft text.</returns>
    public static string ValidationCodes(string? message)
    {
        var text = (message ?? string.Empty)[..Math.Min(message?.Length ?? 0, 20000)];
        (string Marker, string Code)[] rules = [("FAQ", "FAQ"), ("repeated paragraph", "RepeatedParagraph"), ("meta-commentary", "MetaCommentary"), ("navigation", "NavigationText"), ("CTA", "CallToAction"), ("hook", "OpeningHook"), ("worked example", "WorkedExample"), ("real numbers", "ConcreteEvidence"), ("step-by-step", "ActionSteps"), ("keyword", "TopicAlignment"), ("minimum", "MinimumLength"), ("synthetic", "SyntheticDraft"), ("What You'll Learn", "Checklist"), ("Monetization", "ToolsSection")];
        var codes = rules.Where(r => text.Contains(r.Marker, StringComparison.OrdinalIgnoreCase)).Select(r => r.Code).Distinct().ToArray();
        return codes.Length == 0 ? "OtherValidationRule" : string.Join(", ", codes);
    }

    /// <summary>Classifies exceptions and retains technical identity and stack methods without logging raw messages or exception bodies.</summary>
    /// <param name="exception">Actual failure; its content-bearing message is never copied.</param>
    /// <returns>A readable cause and next action plus safe technical details.</returns>
    public static WorkflowFailure Failure(Exception exception)
    {
        var root = exception is AggregateException { InnerExceptions.Count: 1 } aggregate ? aggregate.InnerExceptions[0] : exception;
        var code = "StageException";
        var summary = "The stage could not finish because an unexpected error occurred. Review the exception type and stack methods before retrying.";
        if (root is OperationCanceledException) { code = "ProviderTimeout"; summary = "The provider did not finish before the request ended. Check the connection and model availability before retrying."; }
        else if (root is System.Text.Json.JsonException) { code = "MalformedJson"; summary = "The provider response was not valid JSON. Check the model response format before retrying; no usable output was accepted."; }
        else if (root is HttpRequestException http) { code = "ProviderHttpFailure"; summary = $"The provider rejected the request{(http.StatusCode.HasValue ? $" (HTTP {(int)http.StatusCode.Value})" : "")}. Check the provider connection and account before retrying."; }
        else if (root is InvalidOperationException && root.Message.StartsWith("Article QA blocked:", StringComparison.Ordinal)) { code = "QualityChecksFailed"; summary = $"The article quality checks did not pass ({ValidationCodes(root.Message)}). Review the draft and these checks before retrying."; }
        else if (root is InvalidOperationException && root.Message.Contains("synthetic", StringComparison.OrdinalIgnoreCase)) { code = "SyntheticDraftRejected"; summary = "The writer produced no real article; its diagnostic template was rejected. Check the writing provider before retrying."; }
        else if (root is InvalidOperationException && root.Message.Contains("primary keyword", StringComparison.OrdinalIgnoreCase)) { code = "MissingPrimaryKeyword"; summary = "The approved idea is missing its main keyword. Review the idea's research snapshot before drafting."; }
        else if (root is InvalidOperationException && root.Message.Contains("Ideation returned no usable", StringComparison.Ordinal)) { code = "EmptyIdeation"; summary = "Idea generation returned no usable ideas. Check its provider and response format; generic replacement ideas were not saved."; }
        var methods = string.Join(" <- ", (new StackTrace(root, false).GetFrames() ?? []).Take(12).Select(frame => frame.GetMethod()).Where(method => method is not null).Select(method => $"{method!.DeclaringType?.FullName}.{method.Name}"));
        return new(code, summary, root.GetType().Name, root.HResult, methods);
    }

    /// <summary>Logs one safe provider result with readable consequences and real elapsed time.</summary>
    /// <param name="logger">Existing local logging stack.</param>
    /// <param name="operation">Allowlisted technical operation.</param>
    /// <param name="model">Configured model identifier.</param>
    /// <param name="configurationId">Hash identifying the endpoint configuration.</param>
    /// <param name="started">Monotonic start timestamp.</param>
    /// <param name="outcome">Outcome code, never response content.</param>
    /// <param name="statusCode">Observed HTTP status when available.</param>
    /// <param name="failure">Safely classified actual exception when available.</param>
    public static void ProviderResult(ILogger logger, string operation, string model, string configurationId, long started, string outcome, int? statusCode = null, WorkflowFailure? failure = null)
    {
        var message = outcome switch
        {
            "success" => "The provider returned structured output. Article quality and source support still require checks.",
            "response-received" => "The provider returned text. Response format and quality have not yet been checked.",
            "http-failure" => $"The provider rejected the request (HTTP {statusCode}). Check the local provider connection and account before retrying.",
            "invalid-json" => "The provider response was not valid JSON. Please check the model response format; no usable output was accepted.",
            "cancelled" => "The request was cancelled. Resume it when you are ready; no replacement output was created.",
            "empty-output" => "The provider returned no usable output. Check its response format before retrying; no replacement output was accepted.",
            _ => failure?.Summary ?? "The provider request failed. Check its connection and the recorded failure code before retrying."
        };
        logger.Log(outcome is "success" or "response-received" ? LogLevel.Information : LogLevel.Warning,
            "{Explanation} Provider={Provider} Model={Model} Operation={Operation} ConfigurationId={ConfigurationId} Outcome={Outcome} StatusCode={StatusCode} ElapsedMilliseconds={ElapsedMilliseconds} FailureCode={FailureCode} ExceptionType={ExceptionType} ExceptionCode={ExceptionCode} StackMethods={StackMethods}",
            message, "Ollama", Identifier(model), Identifier(operation), configurationId, outcome, statusCode,
            (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, failure?.Code ?? outcome, failure?.ExceptionType ?? "", failure?.ExceptionCode, failure?.StackMethods ?? "");
    }
}

/// <summary>Safe local failure details without the original exception message, URL, credentials or response body.</summary>
public sealed record WorkflowFailure(string Code, string Summary, string ExceptionType, int ExceptionCode, string StackMethods);
