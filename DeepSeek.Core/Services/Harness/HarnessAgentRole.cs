namespace DeepSeekBrowser.Services.Harness;

/// <summary>
/// Sub-agent / team member role (TUI taxonomy + MetaGPT-style job titles).
/// </summary>
public enum HarnessAgentRoleKind
{
    General,
    Explore,
    Plan,
    Review,
    Implementer,
    Verifier,
    ProductManager,
    Architect,
    Engineer,
    Advocate,
    Critic,
    Custom
}

public sealed class HarnessAgentRoleProfile
{
    public HarnessAgentRoleKind Kind { get; init; }
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string SystemPrompt { get; init; } = "";
    public bool AllowsWrite { get; init; } = true;
    public bool AllowsShell { get; init; } = true;
    public bool AllowsDelegate { get; init; }
}

public static class HarnessAgentRoleRegistry
{
    public static HarnessAgentRoleProfile Resolve(string? roleName)
    {
        var key = Normalize(roleName);
        return key switch
        {
            "explore" or "explorer" or "exploration" => Explore(),
            "plan" or "planning" or "awaiter" => Plan(),
            "review" or "reviewer" or "code-review" => Review(),
            "implementer" or "implement" or "implementation" or "builder" => Implementer(),
            "verifier" or "verify" or "verification" or "validator" or "tester" => Verifier(),
            "pm" or "product-manager" or "productmanager" => ProductManager(),
            "architect" or "arch" => Architect(),
            "engineer" or "dev" or "developer" => Engineer(),
            "advocate" or "proposer" or "solver" => Advocate(),
            "critic" or "skeptic" or "opponent" or "debater" => Critic(),
            "general" or "worker" or "default" or "general-purpose" or "" => General(),
            _ => General()
        };
    }

    public static HarnessAgentRoleProfile General() => new()
    {
        Kind = HarnessAgentRoleKind.General,
        Id = "general",
        DisplayName = "General",
        AllowsWrite = true,
        AllowsShell = true,
        AllowsDelegate = false,
        SystemPrompt =
            "You are a sub-agent (general worker). Complete the assigned task in the workspace. " +
            "Stay scoped; report findings and file paths. Minimum necessary edits."
    };

    public static HarnessAgentRoleProfile Explore() => new()
    {
        Kind = HarnessAgentRoleKind.Explore,
        Id = "explore",
        DisplayName = "Explorer",
        AllowsWrite = false,
        AllowsShell = true,
        SystemPrompt =
            "You are an Explorer sub-agent (read-only). Map the codebase: list_dir, grep, read_file. " +
            "Return path:line evidence. Do not write or edit files."
    };

    public static HarnessAgentRoleProfile Plan() => new()
    {
        Kind = HarnessAgentRoleKind.Plan,
        Id = "plan",
        DisplayName = "Planner",
        AllowsWrite = false,
        AllowsShell = false,
        SystemPrompt =
            "You are a Planner sub-agent. Analyze requirements and produce a structured plan " +
            "(markdown checklist). Do not implement; use UpdatePlan when helpful."
    };

    public static HarnessAgentRoleProfile Review() => new()
    {
        Kind = HarnessAgentRoleKind.Review,
        Id = "review",
        DisplayName = "Reviewer",
        AllowsWrite = false,
        AllowsShell = false,
        SystemPrompt =
            "You are a Reviewer sub-agent. Audit changes for bugs, security, and style. " +
            "Severity: critical / major / minor. Do not patch files — describe fixes for the lead agent."
    };

    public static HarnessAgentRoleProfile Implementer() => new()
    {
        Kind = HarnessAgentRoleKind.Implementer,
        Id = "implementer",
        DisplayName = "Implementer",
        AllowsWrite = true,
        AllowsShell = true,
        SystemPrompt =
            "You are an Implementer sub-agent. Land the specified change with minimal diff. " +
            "Run quick verification (tests/build) when possible before handing back."
    };

    public static HarnessAgentRoleProfile Verifier() => new()
    {
        Kind = HarnessAgentRoleKind.Verifier,
        Id = "verifier",
        DisplayName = "Verifier",
        AllowsWrite = false,
        AllowsShell = true,
        SystemPrompt =
            "You are a Verifier sub-agent. Run validation commands and report pass/fail with logs. " +
            "Do not fix failures — summarize root cause for the lead agent."
    };

    public static HarnessAgentRoleProfile ProductManager() => new()
    {
        Kind = HarnessAgentRoleKind.ProductManager,
        Id = "product-manager",
        DisplayName = "Product Manager",
        AllowsWrite = false,
        AllowsShell = false,
        SystemPrompt =
            "You are the Product Manager (MetaGPT-style). Turn the user goal into a concise PRD: " +
            "goals, user stories, acceptance criteria, out-of-scope. No code changes."
    };

    public static HarnessAgentRoleProfile Architect() => new()
    {
        Kind = HarnessAgentRoleKind.Architect,
        Id = "architect",
        DisplayName = "Architect",
        AllowsWrite = false,
        AllowsShell = true,
        SystemPrompt =
            "You are the Architect. From the PRD/context, produce technical design: components, " +
            "data flow, file touch list, risks. Read-only exploration allowed; no implementation."
    };

    public static HarnessAgentRoleProfile Engineer() => new()
    {
        Kind = HarnessAgentRoleKind.Engineer,
        Id = "engineer",
        DisplayName = "Engineer",
        AllowsWrite = true,
        AllowsShell = true,
        AllowsDelegate = true,
        SystemPrompt =
            "You are the Engineer. Implement the approved design in the workspace. " +
            "Prefer edit over write; keep commits small and traceable."
    };

    public static HarnessAgentRoleProfile Advocate() => new()
    {
        Kind = HarnessAgentRoleKind.Advocate,
        Id = "advocate",
        DisplayName = "Advocate",
        AllowsWrite = false,
        AllowsShell = false,
        SystemPrompt =
            "You are the Advocate (CAMEL-style). Propose a concrete solution or design for the task. " +
            "Cite evidence from the workspace when possible. Be direct and constructive."
    };

    public static HarnessAgentRoleProfile Critic() => new()
    {
        Kind = HarnessAgentRoleKind.Critic,
        Id = "critic",
        DisplayName = "Critic",
        AllowsWrite = false,
        AllowsShell = false,
        SystemPrompt =
            "You are the Critic (CAMEL-style). Challenge the Advocate's proposal: gaps, risks, " +
            "alternatives, and missing tests. Do not implement — refine the direction for the next round."
    };

    public static IReadOnlyList<string> AcceptedRoleNames() =>
    [
        "general", "explore", "plan", "review", "implementer", "verifier",
        "product-manager", "architect", "engineer", "pm", "advocate", "critic"
    ];

    private static string Normalize(string? role) =>
        (role ?? "").Trim().ToLowerInvariant().Replace(" ", "-", StringComparison.Ordinal);
}
