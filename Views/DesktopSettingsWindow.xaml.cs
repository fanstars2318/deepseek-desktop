using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;
using DeepSeekBrowser.Services.ApiManagement;
using DeepSeekBrowser.Services.Harness;
namespace DeepSeekBrowser.Views;

public partial class DesktopSettingsWindow : System.Windows.Window
{
    private readonly McpHub _mcpHub;
    private ObservableCollection<McpServerConfig> _mcpItems;

    public AppConfig? Config { get; private set; }

    public DesktopSettingsWindow(AppConfig config, McpHub mcpHub)
    {
        InitializeComponent();
        _mcpHub = mcpHub;
        Config = config;
        _mcpItems = new ObservableCollection<McpServerConfig>(config.McpServers);
        PortBox.Text = config.LocalApiPort.ToString();
        MaxStepsBox.Text = config.MaxAgentSteps.ToString();
        MaxSubStepsBox.Text = config.MaxSubAgentSteps.ToString();
        EnableSubAgentsCheck.IsChecked = config.EnableSubAgents;
        EnableTeamWorkflowCheck.IsChecked = config.EnableTeamWorkflow;
        EnableParallelExploreCheck.IsChecked = config.EnableParallelExplore;
        EnableDebateWorkflowCheck.IsChecked = config.EnableDebateWorkflow;
        AgentAutoIntentRoutingCheck.IsChecked = config.AgentAutoIntentRouting;
        AgentIntentUseLlmPlannerCheck.IsChecked = config.AgentIntentUseLlmPlanner;
        MaxConcurrentSubAgentsBox.Text = config.MaxConcurrentSubAgents.ToString();
        ParallelExploreFanOutBox.Text = config.ParallelExploreFanOut.ToString();
        DebateMaxRoundsBox.Text = config.DebateMaxRounds.ToString();
        StructuredTraceCheck.IsChecked = config.AgentStructuredTraceEnabled;
        TraceRetentionBox.Text = config.AgentTraceRetentionDays.ToString();
        SemanticMemoryCheck.IsChecked = config.AgentSemanticMemoryEnabled;
        SemanticMemoryAutoCheck.IsChecked = config.AgentSemanticMemoryAutoExtract;
        SemanticTopKBox.Text = config.AgentSemanticMemoryTopK.ToString();
        EmbeddingModelBox.Text = config.AgentEmbeddingModel ?? "";
        AgentStrategyCombo.Items.Clear();
        AgentStrategyCombo.Items.Add(new ComboBoxItem { Content = "Execute · 直接执行", Tag = AgentStrategies.Execute });
        AgentStrategyCombo.Items.Add(new ComboBoxItem { Content = "Blueprint · 探索后出方案", Tag = AgentStrategies.Blueprint });
        AgentStrategyCombo.Items.Add(new ComboBoxItem { Content = "Team · 梦之队", Tag = AgentStrategies.Team });
        AgentStrategyCombo.Items.Add(new ComboBoxItem { Content = "Parallel Explore", Tag = AgentStrategies.ParallelExplore });
        AgentStrategyCombo.Items.Add(new ComboBoxItem { Content = "Debate · 辩论", Tag = AgentStrategies.Debate });
        SelectStrategyCombo(config.DefaultAgentStrategy);
        LocalApiUrlText.Text = $"http://127.0.0.1:{config.LocalApiPort}/v1";
        ConfigPathText.Text = ConfigStore.ConfigFilePath;
        WebTokenStatus.Text = string.IsNullOrWhiteSpace(config.WebUserToken) ? "未登录" : "已登录";
        WebTokenStatus.Foreground = string.IsNullOrWhiteSpace(config.WebUserToken)
            ? (Brush)FindResource("DsDanger")
            : (Brush)FindResource("DsSuccess");
        BindAgentHarnessSettings(config);
        BindDsdApiSummary(config);
        RefreshMcpList();
    }

    private void BindDsdApiSummary(AppConfig config)
    {
        DsdOpenAiCompat.EnsureDefaultMappings(config);
        var maps = config.ModelMappings.Count;
        var keys = config.LocalApiKeys.Count;
        var mode = string.Equals(config.DsdApiSessionMode, "multi", StringComparison.OrdinalIgnoreCase)
            ? "多轮" : "单轮";
        var apiCount = AccountCredentials.CountActiveAccounts("deepseek", config);
        DsdApiSummaryText.Text =
            $"内嵌 API 管理 · {DsdOpenAiCompat.DefaultModel} · 会话 {mode} · {maps} 条模型别名" +
            (config.EnableLocalApiKeyAuth ? " · API Key 认证已启用" : "") +
            $" · DeepSeek API 账户 {apiCount} 个（手动添加）";
    }

    private void BindAgentHarnessSettings(AppConfig config)
    {
        TuiPortInfoText.Text =
            "DSD Harness（C# 进程内）· Blueprint / Execute 工作流\n" +
            "LLM：网页桥 + API 管理（网页 Token）\n" +
            "工具：内置文件/Shell + MCP · 配置：~/.deepseek/config.toml";
        TuiSourcePathBox.Text = "";
        TuiSourcePathBox.IsEnabled = false;
        AgentWorkspaceBox.Text = config.AgentWorkspaceRoot ?? "";
        AgentAllowShellCheck.IsChecked = config.AgentAllowShell;
        AdaptiveOutputCheck.IsChecked = config.EnableAdaptiveOutputEscalation;
        AgentApprovalCombo.Items.Clear();
        AgentApprovalCombo.Items.Add("智能（只读自动）");
        AgentApprovalCombo.Items.Add("只读自动");
        AgentApprovalCombo.Items.Add("全部需确认");
        AgentApprovalCombo.Items.Add("从不确认（不安全）");
        AgentApprovalCombo.SelectedIndex = config.AgentApprovalMode?.ToLowerInvariant() switch
        {
            "readonly" => 1,
            "always" => 2,
            "never" => 3,
            _ => 0
        };
    }

    private void RefreshMcpList()
    {
        McpList.Children.Clear();

        if (_mcpItems.Count == 0)
        {
            McpList.Children.Add(new TextBlock
            {
                Text = "尚未添加 MCP 服务器。点击列表下方「添加 MCP 服务器」，连接成功后 Agent 可动态发现并调用工具。",
                Style = (Style)FindResource("DsHint"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });
        }

        foreach (var server in _mcpItems)
            McpList.Children.Add(BuildServerCard(server));
    }

    private Border BuildServerCard(McpServerConfig server)
    {
        var rowStyle = (Style)FindResource("DsMcpRow");
        var bodyStyle = (Style)FindResource("DsBody");
        var captionBrush = (Brush)FindResource("DsCaptionBrush");
        var success = (Brush)FindResource("DsSuccess");
        var muted = (Brush)FindResource("DsCaptionBrush");
        var ghostStyle = (Style)FindResource("DsGhostButton");
        var switchStyle = (Style)FindResource("DsSwitch");

        var connected = server.Enabled && _mcpHub.IsConnected(server.Id);
        var toolCount = _mcpHub.GetToolCount(server.Id);

        var card = new Border { Style = rowStyle, Margin = new Thickness(0, 0, 0, 10) };
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var dot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = connected ? success : muted,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 6, 10, 0)
        };
        Grid.SetRow(dot, 0);
        Grid.SetColumn(dot, 0);

        var info = new StackPanel();
        info.Children.Add(new TextBlock
        {
            Text = server.Name,
            Style = bodyStyle,
            FontWeight = FontWeights.SemiBold
        });

        var transport = server.IsRemote ? "HTTP/SSE（远程）" : "stdio（本机）";
        info.Children.Add(new TextBlock
        {
            Text = $"{transport} · {Truncate(server.DisplayEndpoint, 40)}",
            FontSize = 11,
            Foreground = captionBrush,
            Margin = new Thickness(0, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var status = new TextBlock { FontSize = 11, Margin = new Thickness(0, 4, 0, 0) };
        if (!server.Enabled)
        {
            status.Text = "已禁用";
            status.Foreground = captionBrush;
        }
        else if (connected)
        {
            status.Text = $"已连接 · {toolCount} 个工具可用（动态能力发现）";
            status.Foreground = success;
        }
        else
        {
            status.Text = "未连接 — 请打开开关或点击「连接全部」";
            status.Foreground = captionBrush;
        }

        info.Children.Add(status);
        Grid.SetRow(info, 0);
        Grid.SetColumn(info, 1);

        var actions = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(18, 10, 0, 0)
        };
        Grid.SetRow(actions, 1);
        Grid.SetColumn(actions, 0);
        Grid.SetColumnSpan(actions, 2);

        var toolsBtn = new Button
        {
            Content = "工具",
            Style = ghostStyle,
            MinWidth = 48,
            MinHeight = 28,
            Padding = new Thickness(8, 0, 8, 0),
            Margin = new Thickness(0, 0, 6, 0),
            IsEnabled = connected && toolCount > 0
        };
        toolsBtn.Click += async (_, _) => await ShowToolsAsync(server);

        var toggle = new ToggleButton
        {
            Style = switchStyle,
            IsChecked = server.Enabled,
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "启用后作为 MCP Client 连接此 Server"
        };
        toggle.Click += async (_, _) => await OnServerToggleAsync(server, toggle);

        var edit = new Button
        {
            Content = "编辑",
            Style = ghostStyle,
            MinWidth = 48,
            MinHeight = 28,
            Padding = new Thickness(8, 0, 8, 0),
            Margin = new Thickness(0, 0, 6, 0)
        };
        edit.Click += (_, _) => EditServer(server);

        var remove = new Button
        {
            Content = "删除",
            Style = ghostStyle,
            MinWidth = 48,
            MinHeight = 28,
            Padding = new Thickness(8, 0, 8, 0),
            Foreground = (Brush)FindResource("DsDanger")
        };
        remove.Click += async (_, _) => await RemoveServerAsync(server);

        actions.Children.Add(toolsBtn);
        actions.Children.Add(toggle);
        actions.Children.Add(edit);
        actions.Children.Add(remove);

        grid.Children.Add(dot);
        grid.Children.Add(info);
        grid.Children.Add(actions);
        card.Child = grid;
        return card;
    }

    private async Task ShowToolsAsync(McpServerConfig server)
    {
        try
        {
            var tools = await _mcpHub.ListToolNamesAsync(server.Id, CancellationToken.None);
            if (tools.Count == 0)
            {
                DsMessageDialog.Info(this, "当前没有可用工具。", server.Name);
                return;
            }

            new McpToolsWindow(server.Name, tools) { Owner = this }.ShowDialog();
        }
        catch (Exception ex)
        {
            DsMessageDialog.Warning(this, ex.Message, "工具列表");
        }
    }

    private async Task OnServerToggleAsync(McpServerConfig server, ToggleButton toggle)
    {
        server.Enabled = toggle.IsChecked == true;
        try
        {
            if (server.Enabled)
            {
                if (!_mcpHub.IsConnected(server.Id))
                    await _mcpHub.ConnectAsync(server, _ => { }, CancellationToken.None);
            }
            else
                await _mcpHub.DisconnectAsync(server.Id, CancellationToken.None);
        }
        catch (Exception ex)
        {
            DsMessageDialog.Warning(this, ex.Message, server.Name);
            server.Enabled = false;
            toggle.IsChecked = false;
        }

        RefreshMcpList();
    }

    private void EditServer(McpServerConfig server)
    {
        var dlg = new McpServerEditorWindow(server) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result is not null)
        {
            var idx = _mcpItems.IndexOf(server);
            if (idx >= 0) _mcpItems[idx] = dlg.Result;
            RefreshMcpList();
        }
    }

    private async Task RemoveServerAsync(McpServerConfig server)
    {
        if (!DsMessageDialog.Confirm(this, $"确定删除 MCP 服务器「{server.Name}」？", "删除", "删除", "取消"))
            return;

        try { await _mcpHub.DisconnectAsync(server.Id, CancellationToken.None); }
        catch { /* ignore */ }

        _mcpItems.Remove(server);
        RefreshMcpList();
    }

    private void AddMcp_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new McpServerEditorWindow { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result is not null)
        {
            _mcpItems.Add(dlg.Result);
            RefreshMcpList();
        }
    }

    private void CopyConfigPath_Click(object sender, RoutedEventArgs e) =>
        Clipboard.SetText(ConfigStore.ConfigFilePath);

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(ConfigStore.ConfigDirectory);
        if (!File.Exists(ConfigStore.ConfigFilePath))
            ConfigStore.Save(Config ?? new AppConfig { McpServers = _mcpItems.ToList() });

        Process.Start(new ProcessStartInfo
        {
            FileName = ConfigStore.ConfigFilePath,
            UseShellExecute = true
        });
    }

    private async void ConnectAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _mcpHub.DisconnectAllAsync(CancellationToken.None);
            var errors = await _mcpHub.ConnectEnabledAsync(_mcpItems, _ => { }, CancellationToken.None);
            var totalTools = _mcpItems.Where(s => s.Enabled).Sum(s => _mcpHub.GetToolCount(s.Id));

            if (Config is not null)
            {
                Config.McpServers = _mcpItems.ToList();
                ConfigStore.Save(Config);
            }

            RefreshMcpList();
            var summary = $"已连接 {_mcpHub.ConnectedCount} 个 MCP Server，共发现 {totalTools} 个工具。";
            if (errors.Count > 0)
                summary += "\n\n失败:\n" + string.Join("\n", errors);

            if (errors.Count > 0)
                DsMessageDialog.Warning(this, summary, "MCP");
            else
                DsMessageDialog.Info(this, summary, "MCP");
        }
        catch (Exception ex)
        {
            RefreshMcpList();
            DsMessageDialog.Warning(this, ex.Message, "MCP");
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (Config is null) Config = new AppConfig();
        if (int.TryParse(PortBox.Text, out var port))
            Config.LocalApiPort = Math.Clamp(port, 1024, 65535);
        if (int.TryParse(MaxStepsBox.Text, out var steps))
            Config.MaxAgentSteps = Math.Clamp(steps, 1, 100);
        if (int.TryParse(MaxSubStepsBox.Text, out var subSteps))
            Config.MaxSubAgentSteps = Math.Clamp(subSteps, 1, 50);
        if (int.TryParse(MaxConcurrentSubAgentsBox.Text, out var maxConcurrent))
            Config.MaxConcurrentSubAgents = Math.Clamp(maxConcurrent, 1, 10);
        if (int.TryParse(ParallelExploreFanOutBox.Text, out var fanOut))
            Config.ParallelExploreFanOut = Math.Clamp(fanOut, 1, Config.MaxConcurrentSubAgents);
        if (int.TryParse(DebateMaxRoundsBox.Text, out var debateRounds))
            Config.DebateMaxRounds = Math.Clamp(debateRounds, 1, 8);
        Config.EnableSubAgents = EnableSubAgentsCheck.IsChecked == true;
        Config.EnableTeamWorkflow = EnableTeamWorkflowCheck.IsChecked == true;
        Config.EnableParallelExplore = EnableParallelExploreCheck.IsChecked == true;
        Config.EnableDebateWorkflow = EnableDebateWorkflowCheck.IsChecked == true;
        Config.AgentAutoIntentRouting = AgentAutoIntentRoutingCheck.IsChecked == true;
        Config.AgentIntentUseLlmPlanner = AgentIntentUseLlmPlannerCheck.IsChecked == true;
        Config.DefaultAgentStrategy = GetSelectedStrategy();
        Config.AgentWorkspaceRoot = AgentWorkspaceBox.Text.Trim();
        Config.AgentAllowShell = AgentAllowShellCheck.IsChecked == true;
        Config.EnableAdaptiveOutputEscalation = AdaptiveOutputCheck.IsChecked == true;
        Config.AgentApprovalMode = AgentApprovalCombo.SelectedIndex switch
        {
            1 => "readonly",
            2 => "always",
            3 => "never",
            _ => "smart"
        };
        Config.AgentAutoApproveReadOnly = Config.AgentApprovalMode is "smart" or "readonly";
        if (int.TryParse(TraceRetentionBox.Text, out var traceDays))
            Config.AgentTraceRetentionDays = Math.Max(0, traceDays);
        Config.AgentStructuredTraceEnabled = StructuredTraceCheck.IsChecked == true;
        Config.AgentSemanticMemoryEnabled = SemanticMemoryCheck.IsChecked == true;
        Config.AgentSemanticMemoryAutoExtract = SemanticMemoryAutoCheck.IsChecked == true;
        if (int.TryParse(SemanticTopKBox.Text, out var topK))
            Config.AgentSemanticMemoryTopK = Math.Clamp(topK, 1, 32);
        Config.AgentEmbeddingModel = EmbeddingModelBox.Text.Trim();
        Config.McpServers = _mcpItems.ToList();
        AgentDesktopConfigSync.Apply(Config);
        ConfigStore.Save(Config);
        DialogResult = true;
        Close();
    }

    private void OpenDeepSeekHome_Click(object sender, RoutedEventArgs e)
    {
        var dir = AgentDesktopConfigSync.HomeDirectory;
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    private void RunDeepSeekDoctor_Click(object sender, RoutedEventArgs e)
    {
        if (Config is not null)
            AgentDesktopConfigSync.Apply(Config);
        var webLoggedIn = !string.IsNullOrWhiteSpace(Config?.WebUserToken);
        var apiCount = Config is null ? 0 : AccountCredentials.CountActiveAccounts("deepseek", Config);
        var text =
            $"Harness: native (in-process)\n普通对话网页登录: {(webLoggedIn ? "是" : "否")}\nAPI 管理账户: {apiCount}\n~/.deepseek: {AgentDesktopConfigSync.ConfigPath}";
        DsMessageDialog.Info(this, text, "Agent 状态");
    }

    private void BuildDeepSeekTuiFromSource_Click(object sender, RoutedEventArgs e) =>
        DsMessageDialog.Info(this, "Agent 已使用进程内 Harness，无需编译 DeepSeek-TUI。", "Agent 引擎");

    private void OpenDeepSeekDocs_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://deepseek-tui.com/zh/docs",
            UseShellExecute = true
        });
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "—";
        return text.Length <= max ? text : text[..max] + "…";
    }

    private void SelectStrategyCombo(string? strategy)
    {
        var norm = HarnessStrategyResolver.Normalize(strategy);
        for (var i = 0; i < AgentStrategyCombo.Items.Count; i++)
        {
            if (AgentStrategyCombo.Items[i] is ComboBoxItem item
                && string.Equals(item.Tag?.ToString(), norm, StringComparison.OrdinalIgnoreCase))
            {
                AgentStrategyCombo.SelectedIndex = i;
                return;
            }
        }

        AgentStrategyCombo.SelectedIndex = 0;
    }

    private string GetSelectedStrategy()
    {
        if (AgentStrategyCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            return tag;
        return AgentStrategies.Execute;
    }
}
