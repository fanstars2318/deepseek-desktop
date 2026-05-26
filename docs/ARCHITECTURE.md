# DeepSeek Desktop 架构（deepseek_desktop）

## 分层

```mermaid
flowchart TB
  WPF[WPF 壳 MainWindow]
  Host[DesktopAgentHost]
  Pages[DesktopWebHost 双 WebView]
  Core[DeepSeek.Core Harness / DSD API]
  Inject[Assets/inject + agent]

  WPF --> Host
  WPF --> Pages
  Host --> Core
  Pages --> Inject
  Host --> Pages
```

| 层 | 职责 | 禁止 |
|----|------|------|
| **WPF** (`MainWindow`, `Views/`) | 窗口、托盘、WebView 容器、加载遮罩 | 业务编排、工具执行 |
| **Services** | 消息路由、WorkMode、DSD API IPC、注入 | 直接修改 Harness 策略 |
| **DeepSeek.Core** | Agent Harness、配置、DSD API 兼容层 | WPF / WebView2 引用 |
| **Assets** | 预构建 UI、注入脚本 | C# 业务逻辑 |

## 工作模式

- **状态机**：`WorkModeCoordinator`（唯一 mode 源）→ `WorkModeStatePayload.For` → 双 WebView `workModeState`
- **普通页按钮**：`chat-mode-floater.js`（输入区右下角 pill）+ `ChatModeFloaterScript.MinimalMount` 兜底
- **Agent 页按钮**：`Assets/agent/index.html` `#mode-float`（composer 右下角）
- **API 账户**：`provider-accounts.json`（手动添加）；与普通模式 `WebUserToken` 独立
- **切换**：`toggleWorkMode` → `DesktopAgentHost` → `ApplyWorkMode` → `ShowChat` / `ShowAgent`

## 桌面 UX（流畅度）

- **双 WebView 保活**：`ShowChat` / `ShowAgent` 仅显隐 + 对称 CrossFade（~120ms），避免整页重载。
- **Loading 遮罩**：`ChatNavigationPolicy` 仅在跨 path 整页导航时显示；同 path 的 SPA/hash 变化不遮罩。
- **注入**：`InjectScheduler` debounce；`WebInjectService.RunScheduledInjectAsync` 收敛为最多 3 次脚本触发。
- **追踪**：`DesktopUiTrace` → `logs/desktop-ui-trace.log`；模式切换仍用 `work-mode-trace.log`。

## 发布

- 唯一日常入口：`publish/DeepSeek.exe`（`build.ps1`）
- 门禁：`verify-integration.ps1`、`verify-workmode-ui.ps1`、`verify-desktop-smoothness.ps1`、`audit-supply-chain.ps1`、`scan-secrets.ps1`

## 同步策略

- 开发主仓：`deepseek_desktop`（唯一源码真相）
- 归档：`deepseek-edge` 只读参考；**禁止**再运行 `sync-to-deepseek-desktop.ps1`（原 `robocopy /MIR` 会回滚 desktop）

## Harness / 可选能力（默认 vs 配置）

| 能力 | 配置键 | 入口 | 默认 |
|------|--------|------|------|
| Native Harness | `DefaultAgentStrategy` | `DeepSeekHarnessRunner` | **on** |
| Team 工作流 | `EnableTeamWorkflow` | `HarnessTeamOrchestrator` | off |
| Parallel Explore | `EnableParallelExplore` | `HarnessParallelExploreOrchestrator` | off |
| Debate | `EnableDebateWorkflow` | `HarnessDebateOrchestrator` | off |
| Software Factory | strategy | `HarnessSoftwareFactoryOrchestrator` | off |
| Graph 运行器 | — | `HarnessGraphRunner` | 按策略 |
| 子 Agent | `EnableSubAgents` | `HarnessSubAgentService` | 配置 |
| Langfuse / 观测 | env + config | `HarnessRunTracer` | 可选 |
| Composio 工具 | MCP / 外部 | `McpHub` | 按 MCP 配置 |

## 桌面宿主接口（阶段 2 拆分）

| 接口 | 职责 |
|------|------|
| `IDesktopWebHost` | 组合门面（原 `IDdWebPages`） |
| `IDesktopWebSurfaces` | ShowChat / ShowAgent / 注入调度 |
| `IWorkModeBroadcast` | workModeState 广播 |
| `IWebChatBridge` | 网页对话 API |
| `IWebAuthBridge` | Token / 健康检查 |
| `IEmbeddedPageMessenger` | Agent 内嵌页消息 |

详见 [coding-style.md](coding-style.md)。
