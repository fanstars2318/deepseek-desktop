# 第三方组件与许可

| 组件 | 用途 | 来源 / 许可 |
|------|------|-------------|
| **Microsoft WebView2** | 嵌入 Edge 内核 | [WebView2 许可](https://developer.microsoft.com/microsoft-edge/webview2/) |
| **ModelContextProtocol** (NuGet) | MCP 客户端 | 见 NuGet 包元数据 |
| **Microsoft.Data.Sqlite** | 本地存储 | MIT |
| **Chat2API 预构建 UI** | `Assets/chat2api/` Agent 内嵌 API 管理 | 上游 Chat2API 项目构建产物；随桌面端分发，不单独修改其源码 |
| **Agent 静态页** | `Assets/agent/` | 仓库内资源 |
| **注入脚本** | `Assets/inject/` | 仓库内资源 |

发布 zip 仅包含 `publish/` 目录内容及 `DISCLAIMER.md`，不含 `Assets/chat2api-ui/` 源码树。

运行 `scripts/audit-supply-chain.ps1` 可导出当前 NuGet 依赖清单至 `artifacts/audit/`。
