namespace DeepSeekBrowser.Services.Harness;

public static class AgentInferenceModes
{
    public const string Web = "web";
    public const string Api = "api";
}

public static class AgentToolCallingProtocols
{
    public const string Xml = "xml";
    public const string OpenAi = "openai";
}

public static class AgentThinkingDisplayModes
{
    public const string Normal = "normal";
    public const string Lite = "lite";
    public const string Raw = "raw";
}

public static class AgentReasoningEfforts
{
    public const string High = "high";
    public const string Max = "max";

    public static string Normalize(string? value) =>
        string.Equals(value, High, StringComparison.OrdinalIgnoreCase) ? High : Max;
}
