# ContentOS → OpenClaw Agent Dispatch Integration Design

**Date:** 2026-05-17
**Status:** Implementation Phase — Phase 1 & 2 Complete
**Updated:** 2026-05-17 — Phases 1-2 implemented and compiling

---

## 1. Problem Statement

ContentOS has an 18-step V4 article pipeline with specialized agent names (`content-research`, `content-strategy`, `content-writer`, `content-humanizer`, `content-seo`, `content-media`, `content-qa-publisher`, `content-conversion`) defined in `AgentStack.cs`. However, the execution layer (`WorkflowCoordinatorAgent` + `WorkflowTaskDispatcher`) **never dispatches to these agents**. Instead, it builds prompt payloads in C# and calls the LLM inline via `ILlmClient`.

Meanwhile, 8 OpenClaw content agents exist with:
- Generic, unfocused SOUL.md / IDENTITY.md templates (no domain-specific instructions)
- No SYSTEM.md files
- Zero real sessions (only heartbeat traffic)
- No awareness of ContentOS workflow context, pipeline state, or article data structures

**Result:** The two systems are completely disconnected. ContentOS agents consume ~26K tokens/heartbeat for nothing. ContentOS pipeline tasks get no agent specialization benefit.

Additionally, the **dreaming cron jobs** (TopicGeneration, ContentGapAnalysis, TitleRewriting) that periodically generate new ideas run as isolated sessions on the `main` agent using Nemotron. They're also disconnected from the content agents.

---

## 2. Architecture Overview

The design uses **unified API dispatch** via `/v1/chat/completions` for all pipeline steps, including research. ContentOS pre-fetches SERP/evidence data and includes it in the prompt context — no agent tool access needed.

```
┌──────────────────────────────────────────────────────────────────┐
│                        ContentOS App                              │
│                                                                  │
│  NewWorkflowRuntimeEngine                                        │
│    └── NewWorkflowRuntimeDispatcher                              │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │  IOpenClawAgentDispatcher  (NEW)                           │   │
│  │                                                            │   │
│  │  Single dispatch path (all steps):                        │   │
│  │  ├── DispatchAsync(agentId, executionKey, payload)        │   │
│  │  │   POST /v1/chat/completions                            │   │
│  │  │   x-openclaw-agent-id: <mapped-agent-id>               │   │
│  │  │   → Structured JSON output                              │   │
│  │  │                                                        │   │
│  │  Research steps get pre-fetched data:                     │   │
│  │  ├── SERP data from ISerpBenchmarkService                 │   │
│  │  ├── SEO data from SeoDataAggregator                     │   │
│  │  └── Evidence context included in prompt                  │   │
│  └────────────────────────────────────────────────────────────┘   │
│                                                                  │
│  Fallback chain:                                                 │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │ OpenClaw dispatch → retry (x2) → inline ILlmClient       │   │
│  └────────────────────────────────────────────────────────────┘   │
│                                                                  │
└──────────────────────────────┬───────────────────────────────────┘
                               │
                               ▼
┌──────────────────────────────────────────────────────────────────┐
│                    OpenClaw Gateway (port 18789)                  │
│                                                                  │
│  /v1/chat/completions                                            │
│                                                                  │
│  Agent Routing (x-openclaw-agent-id):                            │
│  ┌──────────────────────────────────────────────────────────────┐│
│  │ content-research    → research specialist                    ││
│  │ content-strategy    → strategy specialist                    ││
│  │ content-writer      → writing specialist                    ││
│  │ content-humanizer   → humanization specialist               ││
│  │ content-seo         → SEO specialist                        ││
│  │ content-media       → visual/media specialist               ││
│  │ content-qa-publisher→ QA specialist                          ││
│  │ content-conversion  → conversion specialist                  ││
│  └──────────────────────────────────────────────────────────────┘│
│                                                                  │
│  Each agent:                                                     │
│  - Specialized SYSTEM.md with role definition                    │
│  - Domain-specific instructions                                 │
│  - Structured output format requirements                         │
│  - ContentOS pipeline context in AGENTS.md                      │
│  - Research agent: includes synthesis methodology directives    │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

---

## 3. Integration Model: Unified API Dispatch

After analysis, the right approach is **unified stateless API dispatch** for all pipeline steps, including research. Here's why tool-equipped sessions aren't needed:

### Why Not sessions_spawn for Research?

Initially considered giving the research agent tool access (`web_search`, `memory_search`) via full OpenClaw sessions. However:

1. **ContentOS already gathers SERP data** via `ISerpBenchmarkService` and `SeoDataAggregator`
2. **The agent's value is synthesis, not gathering** — interpreting and structuring research findings
3. **Pre-fetched data gives the agent everything it needs** without tool access
4. **Uniform dispatch is simpler** — one API pattern for all steps, one fallback chain
5. **No session management overhead** — no polling, no result extraction, no timeout handling

### How Research Steps Work

For the `content-research` agent (execution key `research-evidence-differentiation`):

1. ContentOS pre-fetches SERP data via `ISerpBenchmarkService.GetSerpDataAsync()`
2. ContentOS aggregates SEO data via `SeoDataAggregator.AggregateAsync()`
3. All data is included in the context payload sent to the agent
4. The agent's SYSTEM.md includes research synthesis methodology
5. The agent produces structured research findings as JSON

This matches the current architecture where `WorkflowCoordinatorAgent.BuildResearchEvidenceDifferentiationPayloadAsync()` already gathers and structures research data.

---

## 4. Recommended Approach: Unified API Dispatch

### Rationale

ContentOS is a **deterministic pipeline**. Each step has:
- Known inputs (idea, article state, prior artifacts)
- Known outputs (structured JSON artifacts)
- Clear success criteria (artifact format validation)

This is fundamentally a **request/response** pattern. The OpenClaw `/v1/chat/completions` endpoint is the natural fit for **all** steps:

1. **ContentOS controls the pipeline flow** — it decides what runs next, handles retries, persists artifacts
2. **ContentOS provides context** — SERP data, prior artifacts, research findings are all pre-fetched and passed in the prompt
3. **OpenClaw provides agent specialization** — system prompts, workspace context, model routing
4. **Clean separation of concerns** — ContentOS owns data+orchestration; OpenClaw owns LLM interaction quality

### Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| API endpoint | `/v1/chat/completions` | Stateless, OpenAI-compatible, already enabled |
| Agent routing | `x-openclaw-agent-id` header | Maps ContentOS task to specialized agent |
| Context passing | Full context in each request message | No session state to manage; includes pre-fetched SERP data |
| Output format | Structured JSON in system prompt | Agent responds with parseable JSON |
| Error handling | ContentOS retries with backoff | Pipeline already has retry logic |
| Model selection | Agent's configured model + fallbacks | OpenClaw handles model routing |
| Artifact storage | ContentOS DB (unchanged) | No change to persistence model |
| Research data | Pre-fetched by ContentOS | SERP data already gathered by existing services; agent synthesizes |
| Tool access | Not needed | All data provided in context; no agent tools required |

---

## 5. Implementation Plan

### Phase 1: Agent Specialization (OpenClaw Side)

#### 5.1.1 Create SYSTEM.md for Each Content Agent

Each agent needs a domain-specific system prompt that replaces the generic template. The prompt must define:

1. **Role** — What this agent specializes in
2. **Context** — How it fits in the ContentOS pipeline
3. **Input format** — What data it receives
4. **Output format** — Structured JSON schema it must produce
5. **Quality criteria** — What "done right" looks like

**Example: content-writer SYSTEM.md**

```markdown
# Content Writer Agent

You are the **Long-form SEO Writer** in the ContentOS article pipeline.

## Role
Write high-quality, rule-compliant long-form articles optimized for SEO
and reader value. You receive a content brief and produce a complete article
draft.

## Pipeline Position
You are step 3 of the pipeline, receiving:
- Content strategy brief (from content-strategy)
- Research evidence (from content-research)
- Article outline (from content-strategy)

And producing:
- A complete long-form article draft

## Input Format
You will receive a JSON object containing:
- `title`: Article title
- `primaryKeyword`: Primary SEO target keyword
- `secondaryKeywords`: Supporting keywords
- `searchIntent`: Informational, transactional, etc.
- `audiencePainPoint`: Reader's core problem
- `audienceGoal`: What the reader wants to achieve
- `recommendedAngle`: Unique angle to take
- `whyNow`: Timeliness hook
- `outline`: Section structure with headings
- `targetWordCountMin/Max`: Word count targets

## Output Format
Respond with ONLY a JSON object:
```json
{
  "title": "...",
  "slug": "...",
  "summary": "...",
  "metaDescription": "...",
  "introParagraphs": ["...", "...", "..."],
  "sections": [
    { "heading": "...", "paragraphs": ["...", "..."] }
  ],
  "conclusionParagraphs": ["...", "..."],
  "callToAction": "..."
}
```

## Quality Criteria
- Target word count must be within range
- Every section heading must be reader-beneficial
- No keyword stuffing — use natural variations
- Include practical, actionable advice
- Avoid AI-typical phrases and patterns
- Write at a 7th-8th grade reading level
```

**Example: content-research SYSTEM.md**

```markdown
# Content Research Agent

You are the **Research & Evidence Specialist** in the ContentOS article pipeline.

## Role
Synthesize pre-fetched research data into structured evidence and differentiation
insights. You do NOT gather data — it is provided to you. Your job is to interpret,
prioritize, and structure it.

## Pipeline Position
You are step 2 of the pipeline, receiving:
- Idea metadata (title, keywords, audience, intent)
- Pre-fetched SERP data (competitor titles, search intents, related queries)
- SEO aggregated data (keyword difficulty, content gaps, ranking opportunities)

And producing:
- Structured research evidence
- Differentiation angles
- Audience insight summary

## Input Format
You will receive a JSON object containing:
- `idea`: Idea metadata (title, primaryKeyword, searchIntent, audiencePainPoint, etc.)
- `serpData`: Pre-fetched SERP results and competitor analysis
- `seoData`: Aggregated SEO metrics and opportunities
- `sourceSummaryJson`: Source keywords and intent signals

## Output Format
Respond with ONLY a JSON object:
```json
{
  "evidenceSummary": "...",
  "differentiationAngles": ["...", "..."],
  "audienceInsights": {
    "corePainPoints": ["...", "..."],
    "searchBehaviors": ["...", "..."],
    "contentGaps": ["...", "..."]
  },
  "competitorAnalysis": {
    "commonTopics": ["...", "..."],
    "missingAngles": ["...", "..."],
    "opportunitySignals": ["...", "..."]
  },
  "recommendationNotes": "..."
}
```

## Quality Criteria
- Ground all claims in the provided SERP/SEO data
- Identify at least 3 differentiation angles
- Highlight content gaps competitors are missing
- Avoid generic observations — be specific and actionable
- Flag any data limitations or low-confidence findings
```

Similar SYSTEM.md files need to be created for all 8 content agents.

#### 5.1.2 Agent-to-Task Mapping

| Agent ID | V4 Pipeline Step(s) | Execution Key(s) |
|----------|---------------------|-------------------|
| `content-strategy` | Content Strategy & Briefing | `content-strategy-briefing` |
| `content-research` | Research, Evidence & Differentiation | `research-evidence-differentiation` |
| `content-writer` | Long-form Drafting | `draft-article`, `longform-drafting` |
| `content-humanizer` | Humanization & Editorial Polish, Anti-AI Pattern Detection, Final Human Editor Pass | `humanization-editorial-polish`, `anti-ai-pattern-detection`, `final-human-editor-pass` |
| `content-seo` | CRAFT Review/Edit/Optimize, Internal Link Intelligence, SEO optimization steps | `craft-review-edit-optimize`, `internal-link-intelligence`, `seo-linking`, `optimized-seo-package` |
| `content-media` | Add Images/Visuals/Media, Generate Visual Assets, Original Asset Creation | `craft-add-images-visuals-media`, `generate-visual-assets`, `original-asset-creation` |
| `content-qa-publisher` | CRAFT Fact-Check & Trust Check, Final QA & Compliance, Content Scorecard | `craft-fact-check-trust-check`, `final-qa-compliance`, `content-scorecard`, `strict-qa`, `light-qa` |
| `content-conversion` | CRAFT Cut the Fluff, CRAFT Trust-Build, Monetization & CTA, Pre-QA Normalization | `craft-cut-fluff`, `craft-trust-build`, `monetization-cta`, `pre-qa-normalization` |

### Phase 2: Dispatch Interface (ContentOS Side)

#### 5.2.1 New Interface: `IOpenClawAgentDispatcher`

```csharp
public interface IOpenClawAgentDispatcher
{
    /// <summary>
    /// Dispatch a ContentOS pipeline task to the appropriate OpenClaw agent.
    /// Uses /v1/chat/completions with x-openclaw-agent-id routing.
    /// Research steps include pre-fetched SERP/SEO data in the payload.
    /// </summary>
    Task<AgentDispatchResult> DispatchAsync(
        string agentId,
        string executionKey,
        object contextPayload,
        CancellationToken cancellationToken = default);
}

public sealed record AgentDispatchResult(
    bool Success,
    string? RawResponse,
    object? ParsedOutput,
    string? Error,
    int InputTokens,
    int OutputTokens,
    string ModelUsed,
    TimeSpan Duration
);
```

#### 5.2.2 Implementation: `OpenClawAgentDispatcher`

```csharp
public class OpenClawAgentDispatcher : IOpenClawAgentDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenClawAgentDispatcher> _logger;
    private readonly OpenClawDispatchOptions _options;

    public async Task<AgentDispatchResult> DispatchAsync(
        string agentId,
        string executionKey,
        object contextPayload,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = BuildSystemPrompt(agentId, executionKey);
        var userMessage = JsonSerializer.Serialize(contextPayload, JsonOptions);

        var request = new
        {
            model = "openclaw",  // Routes to the agent's configured model
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            },
            temperature = 0.3,  // Low temperature for structured output
            max_tokens = 8192
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("x-openclaw-agent-id", agentId);
        httpRequest.Headers.Add("Authorization", $"Bearer {_options.ApiToken}");

        var sw = Stopwatch.StartNew();
        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        sw.Stop();

        // Parse response, extract structured output, return result
        // ...
    }
}
```

#### 5.2.3 Configuration

```csharp
public sealed class OpenClawDispatchOptions
{
    public bool Enabled { get; set; } = false;
    public string BaseUrl { get; set; } = "http://127.0.0.1:18789";
    public string ApiToken { get; set; } = "";  // From OPENCLAW_GATEWAY_TOKEN
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxRetries { get; set; } = 2;
    public TimeSpan RetryBackoff { get; set; } = TimeSpan.FromSeconds(3);
    public Dictionary<string, string> AgentOverrides { get; set; } = new();  // Per-key model overrides
}
```

Registered in DI:

```csharp
services.Configure<OpenClawDispatchOptions>(config.GetSection("OpenClaw"));
services.AddHttpClient<IOpenClawAgentDispatcher, OpenClawAgentDispatcher>(client =>
{
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = options.RequestTimeout;
});
```

### Phase 3: Integration Wiring

#### 5.3.1 Dispatcher Switch

Modify `WorkflowTaskDispatcher.DispatchAsync` to route through `IOpenClawAgentDispatcher` when enabled:

```csharp
public async Task<WorkflowTaskExecutionResult> DispatchAsync(
    ContentWorkflowTask task, ContentIdea? idea,
    GeneratedLongformArticle article, CancellationToken cancellationToken)
{
    var executionKey = _definitionResolver.Resolve(task).ExecutionKey;

    // Check if OpenClaw agent dispatch is enabled for this execution key
    if (_openClawDispatcher is not null &&
        _openClawOptions.Enabled &&
        TryGetAgentId(task, executionKey, out var agentId))
    {
        var contextPayload = await BuildContextPayload(
            task, idea, article, executionKey, cancellationToken);
        var result = await _openClawDispatcher.DispatchAsync(
            agentId, executionKey, contextPayload, cancellationToken);

        if (result.Success)
        {
            return MapToWorkflowResult(task, result, idea, article);
        }

        // Fall back to inline execution on failure
        _logger.LogWarning("OpenClaw dispatch failed for {Key}, falling back. Error: {Error}",
            executionKey, result.Error);
    }

    // Existing inline dispatch logic (unchanged)
    // ...
}
```

#### 5.3.2 Research Data Pre-Fetching

For the `content-research` agent, ContentOS pre-fetches SERP/evidence data
and includes it in the context payload, rather than relying on the agent to gather it.

```csharp
private async Task<object> BuildContextPayloadAsync(
    ContentWorkflowTask task, ContentIdea? idea,
    GeneratedLongformArticle article, string executionKey,
    CancellationToken ct)
{
    // For research steps, include pre-fetched SERP data
    if (executionKey == "research-evidence-differentiation" && idea is not null)
    {
        var serpTask = await _serpBenchmarkService.GetSerpDataAsync(
            idea.PrimaryKeyword, ct);
        var seoData = serpTask is not null
            ? await _seoDataAggregator.AggregateAsync(serpTask, ct)
            : null;

        return new
        {
            ExecutionKey = executionKey,
            Idea = idea,
            SerpData = seoData,
            Article = article,
            OutputFormat = "json"
        };
    }

    // For all other steps, standard payload
    return new
    {
        ExecutionKey = executionKey,
        Idea = idea,
        Article = article,
        OutputFormat = "json"
    };
}
```

This ensures the research agent has all the data it needs in the prompt,
without requiring tool access.

#### 5.3.3 Agent ID Resolution

```csharp
private static bool TryGetAgentId(
    ContentWorkflowTask task, string executionKey, out string agentId)
{
    // Map execution key to OpenClaw agent ID
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["content-strategy-briefing"] = "content-strategy",
        ["research-evidence-differentiation"] = "content-research",
        ["draft-article"] = "content-writer",
        ["longform-drafting"] = "content-writer",
        ["humanization-editorial-polish"] = "content-humanizer",
        ["anti-ai-pattern-detection"] = "content-humanizer",
        ["final-human-editor-pass"] = "content-humanizer",
        ["craft-review-edit-optimize"] = "content-seo",
        ["internal-link-intelligence"] = "content-seo",
        ["seo-linking"] = "content-seo",
        ["optimized-seo-package"] = "content-seo",
        ["craft-add-images-visuals-media"] = "content-media",
        ["generate-visual-assets"] = "content-media",
        ["original-asset-creation"] = "content-media",
        ["craft-fact-check-trust-check"] = "content-qa-publisher",
        ["final-qa-compliance"] = "content-qa-publisher",
        ["content-scorecard"] = "content-qa-publisher",
        ["strict-qa"] = "content-qa-publisher",
        ["light-qa"] = "content-qa-publisher",
        ["craft-cut-fluff"] = "content-conversion",
        ["craft-trust-build"] = "content-conversion",
        ["monetization-cta"] = "content-conversion",
        ["pre-qa-normalization"] = "content-conversion"
    };

    return map.TryGetValue(task.AssignedAgent, out agentId!)
        || map.TryGetValue(executionKey, out agentId!);
}
```

#### 5.3.4 Feature Flag

```json
// appsettings.json
{
  "OpenClaw": {
    "Enabled": false,
    "BaseUrl": "http://127.0.0.1:18789",
    "ApiToken": "",
    "RequestTimeout": "00:05:00",
    "MaxRetries": 2,
    "RetryBackoff": "00:00:03",
    "AgentOverrides": {}
  }
}
```

Start with `Enabled: false`. Enable per-task via config or admin UI once validated.

---

## 6. Agent Specialization Design

### 6.1 SYSTEM.md Template Structure

Each content agent SYSTEM.md must include:

```markdown
# [Agent Name] — ContentOS Pipeline Specialist

## Role
[One-line role definition]

## Pipeline Context
[Where this agent sits in the pipeline, what it receives, what it produces]

## Input Schema
[JSON schema of expected input]

## Output Schema
[JSON schema of required output — must be parseable]

## Quality Standards
[Bullet list of what "good" looks like]

## Anti-Patterns
[What to avoid — AI-typical language, keyword stuffing, etc.]

## Constraints
[Word counts, format requirements, etc.]
```

### 6.2 Context Payload Design

Each dispatch sends a **context payload** that includes:

1. **Idea metadata** — title, keywords, audience, intent, angle
2. **Prior artifacts** — relevant artifacts from completed pipeline steps
3. **Task-specific instructions** — the execution key's requirements
4. **Output format specification** — exact JSON schema expected
5. **Pre-fetched data** (research steps only) — SERP data, SEO metrics

This is sent as the user message in the chat completion request.

### 6.3 Fallback Chain

```
OpenClaw agent dispatch attempt
  ↓ (success) → Parse structured JSON → Persist artifact
  ↓ (failure)
  ↓
Retry (up to MaxRetries with backoff)
  ↓ (failure)
  ↓
Fall back to inline ILlmClient execution (current behavior)
  ↓ (failure)
  ↓
Mark task as Failed, queue automatic rewrite if applicable
```

---

## 7. Token Cost Analysis

### Current State (Inline LLM)

| Step | Estimated Input Tokens | Estimated Output Tokens |
|------|----------------------|------------------------|
| Content Strategy | ~4,000 | ~2,000 |
| Research Evidence | ~8,000 | ~3,000 |
| Long-form Draft | ~6,000 | ~5,000 |
| Humanization | ~8,000 | ~5,000 |
| SEO Optimization | ~8,000 | ~3,000 |
| QA | ~8,000 | ~2,000 |
| **Total per article** | **~42,000** | **~20,000** |

### Projected State (OpenClaw Agent Dispatch)

| Component | Tokens per call |
|-----------|----------------|
| Agent SYSTEM.md | ~2,000-3,000 |
| Context payload | ~4,000-8,000 |
| Prior artifacts | ~5,000-10,000 |
| **Total input per step** | **~11,000-21,000** |

The OpenClaw approach adds ~3K tokens per call for the specialized system prompt, but gains:
- Better output quality (domain-specialized prompting)
- Model routing per agent (e.g., Nemotron for research, GLM for writing)
- Consistent output format enforcement

**Net token increase per article: ~18,000-54,000 input tokens (~43-129% increase)**

This is acceptable because:
1. The 8 dormant agents' heartbeat savings (~800K/day) far outweigh the per-article increase
2. Better quality output reduces retry cycles (which currently waste tokens)
3. Specialized prompts reduce hallucination and off-format outputs

---

## 8. Migration Strategy

### Stage 1: Agent Specialization (Week 1)
- Write SYSTEM.md for all 8 content agents
- Update AGENTS.md and IDENTITY.md with ContentOS pipeline context
- Disable heartbeat for dormant agents (change to `target: "none"` or remove heartbeat)
- Test each agent individually via direct `/v1/chat/completions` calls

### Stage 2: Dispatch Interface (Week 2)
- Implement `IOpenClawAgentDispatcher` and `OpenClawAgentDispatcher`
- Add configuration and DI registration
- Feature flag: `OpenClaw.Enabled = false`
- Unit tests for dispatch interface

### Stage 3: Selective Enablement (Week 3)
- Enable OpenClaw dispatch for **one** pipeline step (e.g., `content-strategy-briefing`)
- A/B comparison: OpenClaw agent output vs. inline LLM output
- Validate output format, quality, and consistency

### Stage 4: Full Rollout (Week 4)
- Enable for all V4 pipeline steps
- Monitor token usage, latency, and output quality
- Adjust system prompts based on results
- Remove or deprecate inline LLM paths once validated

---

## 9. Risks and Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|-----------|------------|
| Agent produces unparseable output | High — pipeline breaks | Medium | Strict output format in SYSTEM.md + JSON parsing with fallback regex extraction |
| OpenClaw Gateway unavailable | High — pipeline stalls | Low | Feature flag to fall back to inline LLM; retry with backoff |
| Token cost exceeds budget | Medium | Low | Monitor per-step tokens; set max_tokens limits; use cheaper models for research/SEO steps |
| Agent hallucination in output | Medium | Medium | Output validation layer before artifact persistence; QA step catches issues |
| Context window overflow | High — truncated output | Low | Compress prior artifacts; summarize long inputs; set reasonable token limits |
| Latency increase from HTTP round-trip | Low | High | ContentOS already uses async processing; ~100ms overhead per step is negligible |

---

## 10. Resolved Decisions

### Research Tasks: Pre-Fetched Data

**Decision:** All steps use the same stateless `/v1/chat/completions` dispatch. ContentOS pre-fetches SERP/evidence data via `ISerpBenchmarkService` and `SeoDataAggregator`, and includes it in the prompt context. The agent's value is in *synthesizing* the data, not *gathering* it.

**Rationale:**
- ContentOS already has SERP data gathering infrastructure
- Keeping dispatch uniform reduces complexity
- Pre-fetched data gives the agent everything it needs
- No need for tool access, session management, or polling
- Simpler fallback chain (retry → inline LLM)

### Longform-Drafting: Agentify with Full Context

**Decision:** The `longform-drafting` step routes through the `content-writer` agent. C# code builds the context payload (outline, SERP data, rework directives, prior draft state) — it already does this in `BuildAndWriteDraftAsync`. The agent's SYSTEM.md replaces the prompt template currently hardcoded in C#.

**Rationale:**
- The C# code already assembles all context — we just route it through the agent instead of directly to ILlmClient
- This is the most important pipeline step and deserves agent specialization
- The agent SYSTEM.md handles the complexity currently in C# prompt templates
- If agent dispatch fails, we fall back to inline LLM (same as current behavior)

### Agent Consolidation: Keep 8, Execution-Key-Aware SYSTEM.md

**Decision:** Keep 8 agents. Each agent's SYSTEM.md handles multiple related execution keys by branching on the key. Agents are already logically grouped (humanizer does humanization-adjacent work, conversion does conversion-adjacent work).

**Rationale:**
- More precise system prompts without agent sprawl
- Fewer agents to maintain, configure, and monitor
- Less heartbeat overhead
- Execution key is passed in every request, so the agent knows which sub-role to activate
- Splitting further creates diminishing returns and more config complexity

### Dreaming Cron Integration: Route to Content Agents

**Decision:** Dreaming cron jobs dispatch to the relevant content agents instead of `main`:
- TopicGeneration → `content-strategy` (ideation specialist)
- ContentGapAnalysis → `content-research` (opportunity-finding specialist)
- TitleRewriting → `content-writer` (headline optimization specialist)

The dreaming spec JSON format is included as context in the cron task message, so the agent has both its specialized SYSTEM.md and the dreaming instructions.

**Rationale:**
- Better output quality from domain-specialized prompts
- Content agents earn their heartbeat cost
- No new infrastructure — just change the `agentId` in cron config
- Dreaming prompts are already well-tuned; we add the agent's specialization on top

### Heartbeat Policy: Disable for Content Agents

**Decision:** Disable heartbeat for all 8 content agents. Set `heartbeat.target: "none"` in their config. They only activate when the pipeline dispatches to them.

**Rationale:**
- Content agents are pure pipeline workers — no monitoring, no messages, no periodic awareness needed
- Saves ~417K tokens/day (8 agents × 26K tokens × 2 heartbeats/day)
- Operational agents (main, orchestrator, architect, etc.) keep their heartbeat for awareness tasks
- Agents still get invoked on-demand by dispatch calls; heartbeat is unnecessary overhead

---

## 11. Appendix: Current Agent State

| Agent ID | Purpose | Real Sessions | Heartbeat Only | SYSTEM.md | Workspace |
|----------|---------|--------------|----------------|-----------|-----------|
| content-research | Research & evidence gathering | 0 | ✅ | Generic | workspace-content-research |
| content-strategy | Content strategy & briefing | 0 | ✅ | Generic | workspace-content-strategy |
| content-writer | Long-form article writing | 0 | ✅ | Generic | workspace-content-writer |
| content-humanizer | Humanization & editorial polish | 0 | ✅ | Generic | workspace-content-humanizer |
| content-seo | SEO optimization & linking | 0 | ✅ | Generic | workspace-content-seo |
| content-conversion | Conversion & CTA optimization | 0 | ✅ | Generic | workspace-content-conversion |
| content-media | Visual assets & media planning | 0 | ✅ | Generic | workspace-content-media |
| content-qa-publisher | QA, compliance & scoring | 0 | ✅ | Generic | workspace-content-qa-publisher |

All 8 agents have:
- Generic SOUL.md (OpenClaw default template)
- Empty IDENTITY.md
- No SYSTEM.md
- ~26K input tokens per heartbeat
- Zero production output to date