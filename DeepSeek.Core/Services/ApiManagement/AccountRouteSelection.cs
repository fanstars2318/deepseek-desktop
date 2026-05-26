using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.ApiManagement;

/// <summary>负载均衡选中的供应商 + 账户 + 映射后模型（对齐 DSD API AccountSelection）。</summary>
public sealed class AccountRouteSelection
{
    public required ApiProviderEntry Provider { get; init; }
    public required ProviderAccountRecord Account { get; init; }
    public required string ActualModel { get; init; }
}
