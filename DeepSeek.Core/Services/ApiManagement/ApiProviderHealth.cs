namespace DeepSeekBrowser.Services.ApiManagement;

public sealed class ApiProviderHealth
{
    public bool Online { get; init; }
    public string Message { get; init; } = "";
}
