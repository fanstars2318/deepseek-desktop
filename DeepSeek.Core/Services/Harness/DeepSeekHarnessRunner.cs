using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.Harness.Graph;

namespace DeepSeekBrowser.Services.Harness;

public sealed class DeepSeekHarnessRunner : IHarnessRunner
{
    private readonly Func<IAgentWebChat> _chatFactory;
    private readonly McpHub _mcp;
    private readonly Func<string, string, Task<bool>> _requestApproval;
    private readonly Func<string, string, IReadOnlyList<string>, Task<bool>>? _scopeApproval;
    private readonly IUserQuestionHandler? _userQuestions;

    public DeepSeekHarnessRunner(
        Func<IAgentWebChat> chatFactory,
        McpHub mcp,
        Func<string, string, Task<bool>> requestApproval,
        IUserQuestionHandler? userQuestions = null,
        Func<string, string, IReadOnlyList<string>, Task<bool>>? scopeApproval = null)
    {
        _chatFactory = chatFactory;
        _mcp = mcp;
        _requestApproval = requestApproval;
        _scopeApproval = scopeApproval;
        _userQuestions = userQuestions;
    }

    public Task<HarnessRunResult> RunAsync(
        HarnessRunRequest request,
        HarnessRunCallbacks callbacks,
        CancellationToken ct)
    {
        var workspace = AgentWorkspace.ResolveRoot(request.Config);
        var strategy = request.Strategy;
        string? skillOverride = request.SkillId;
        string? graphOverride = null;

        if (!string.IsNullOrWhiteSpace(request.PlaybookId)
            && HarnessPlaybookRegistry.TryGet(request.PlaybookId, workspace, out var pb)
            && pb is not null)
        {
            var expanded = HarnessPlaybookExpander.Apply(pb, workspace);
            pb = expanded.Playbook;
            if (!string.IsNullOrWhiteSpace(expanded.SkillId))
                skillOverride = expanded.SkillId;
            if (!string.IsNullOrWhiteSpace(expanded.GraphId))
                graphOverride = expanded.GraphId;
            if (!string.IsNullOrWhiteSpace(pb.Strategy))
                strategy = pb.Strategy;
        }

        if (!string.IsNullOrWhiteSpace(graphOverride))
            strategy = AgentStrategies.GraphStrategy(graphOverride);

        var subAgents = new HarnessSubAgentService(
            _chatFactory,
            _mcp,
            _requestApproval,
            _userQuestions,
            _scopeApproval,
            Math.Clamp(request.Config.MaxConcurrentSubAgents, 1, 10));

        if (HarnessGraphStrategy.TryParse(strategy, out var graphId))
        {
            var chat = _chatFactory();
            var approval = new ApprovalGate(request.Config, _requestApproval);
            var permission = new PermissionGate(request.Config, approval, _scopeApproval);
            var graphRequest = CloneRequest(request, strategy, skillOverride);
            return new HarnessGraphRunner(chat, _mcp, permission, subAgents, _userQuestions, workspace)
                .RunAsync(graphId, graphRequest, callbacks, ct, request.ResumeGraphThreadId);
        }

        var workflow = HarnessStrategyResolver.Resolve(strategy).Workflow;
        var routedRequest = CloneRequest(request, strategy, skillOverride);

        if (workflow == HarnessWorkflow.Team)
            return new HarnessTeamOrchestrator(subAgents).RunAsync(routedRequest, callbacks, ct);

        if (workflow == HarnessWorkflow.ParallelExplore)
            return new HarnessParallelExploreOrchestrator(subAgents).RunAsync(routedRequest, callbacks, ct);

        if (workflow == HarnessWorkflow.Debate)
            return new HarnessDebateOrchestrator(subAgents).RunAsync(routedRequest, callbacks, ct);

        var chat2 = _chatFactory();
        var approval2 = new ApprovalGate(request.Config, _requestApproval);
        var permission2 = new PermissionGate(request.Config, approval2, _scopeApproval);
        var orchestrator = new HarnessOrchestrator(chat2, _mcp, permission2, _userQuestions, workspace, subAgents);
        return orchestrator.RunAsync(CloneRequest(request, strategy, skillOverride), callbacks, ct);
    }

    private static HarnessRunRequest CloneRequest(HarnessRunRequest request, string strategy, string? skillId) =>
        new()
        {
            Config = request.Config,
            Prompt = request.Prompt,
            Strategy = strategy,
            ExistingHarnessState = request.ExistingHarnessState,
            WebChatSessionId = request.WebChatSessionId,
            RefFileIds = request.RefFileIds,
            PlaybookId = request.PlaybookId,
            SkillId = skillId ?? request.SkillId,
            AgentSessionId = request.AgentSessionId,
            SubAgentRole = request.SubAgentRole,
            MaxStepsOverride = request.MaxStepsOverride,
            ResumeGraphThreadId = request.ResumeGraphThreadId
        };
}
