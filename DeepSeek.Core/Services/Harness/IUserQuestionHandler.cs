namespace DeepSeekBrowser.Services.Harness;

public sealed class UserQuestionRequest
{
    public required string Question { get; init; }
    public required IReadOnlyList<UserQuestionOption> Options { get; init; }
}

public sealed class UserQuestionOption
{
    public required string Label { get; init; }
    public string? Description { get; init; }
}

public interface IUserQuestionHandler
{
    Task<string> AskAsync(UserQuestionRequest request, CancellationToken ct);
}
