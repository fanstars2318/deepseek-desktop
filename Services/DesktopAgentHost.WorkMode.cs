using System.Text.Json;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services;

public sealed partial class DesktopAgentHost
{
    /// <summary>ApplyWorkMode 执行中；用于抑制 SurfaceChanged 重复广播。</summary>
    public bool IsApplyingWorkMode { get; private set; }

    private async Task NavigateForWorkModeAsync(string targetUrl, string mode) =>
        await ApplyWorkModeAsync(default, mode);

    public Task VerifyWorkModeSwitchAsync(string mode) => ApplyWorkModeAsync(default, mode);

    private Task ApplyWorkModeAsync(JsonElement msg, string mode, bool navigate = true) =>
        RunOnUiAsync(() => ApplyWorkModeOnUiAsync(msg, mode, navigate));

    private static bool IsToggleMessage(JsonElement msg) =>
        msg.ValueKind == JsonValueKind.Object
        && msg.TryGetProperty("type", out var typeEl)
        && typeEl.GetString() is "toggleWorkMode";

    private async Task ApplyWorkModeOnUiAsync(JsonElement msg, string mode, bool navigate)
    {
        await _workModeGate.WaitAsync();
        try
        {
            mode = WorkModeCoordinator.NormalizeMode(mode);
            var toAgent = mode is "agent" or "plan";
            var isToggle = IsToggleMessage(msg);

            if (toAgent == _pages.IsAgentVisible)
            {
                WorkModeTrace.Write($"ApplyWorkMode no-op mode={mode} agentVisible={_pages.IsAgentVisible}");
                return;
            }

            WorkModeTrace.Write($"ApplyWorkMode start mode={mode} agentVisible={_pages.IsAgentVisible} toggle={isToggle}");

            IsApplyingWorkMode = true;
            _pages.WorkMode.CancelScheduledRetries();

            _config.DefaultWorkMode = mode;
            if (mode == "plan")
                _config.DefaultAgentStrategy = AgentStrategies.Blueprint;
            else if (mode == "agent")
                _config.DefaultAgentStrategy = AgentStrategies.Execute;
            _localApi.UpdateConfig(_config);
            _pages.WorkMode.SetModeFromConfig(mode);

            var skipNavigate = isToggle
                               || (TryGetMessageProperty(msg, "skipNavigate", out var snEl)
                                   && snEl.ValueKind == JsonValueKind.True);

            if (toAgent)
            {
                await _pages.WorkMode.ShowAgentSurfaceAsync();
                await _pages.WorkMode.BroadcastImmediateAsync();
                WorkModeTrace.Write($"ApplyWorkMode done agent visible={_pages.IsAgentVisible}");
                QueueAgentSurfaceFollowUp(msg);
                return;
            }

            await _pages.WorkMode.ShowChatSurfaceAsync();
            await _pages.WorkMode.BroadcastImmediateAsync();
            RememberWebChatSessionFromMessage(msg);
            if (navigate && !skipNavigate && NavigateToUrl is not null)
                _ = NavigateToUrl(ResolveChatNavigationUrl());

            WorkModeTrace.Write($"ApplyWorkMode done chat visible={!_pages.IsAgentVisible}");
            QueueChatSurfaceFollowUp(msg, skipBurst: isToggle);
        }
        catch (Exception ex)
        {
            WorkModeTrace.Write("ApplyWorkMode error: " + ex);
            throw;
        }
        finally
        {
            IsApplyingWorkMode = false;
            _workModeGate.Release();
        }
    }
}
