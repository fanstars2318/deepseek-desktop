# DeepSeek Desktop

Windows 桌面客户端：内嵌 DeepSeek 网页会话、本地 **Agent Harness**、**DSD API** 兼容层、MCP 工具、工作区沙盒与自动化能力。

| 项目 | 说明 |
|------|------|
| **仓库** | [github.com/fanstars2318/deepseek-desktop](https://github.com/fanstars2318/deepseek-desktop) |
| **维护者** | [@fanstars2318](https://github.com/fanstars2318)（本仓库代码与发布均由其维护） |
| **当前主线** | WPF + WebView2（`build.ps1` 单入口，无 Qt / Bridge 实验壳） |
| **最新发布** | [Releases](https://github.com/fanstars2318/deepseek-desktop/releases) |

> **文档说明**：本 README、`docs/INSTALL.md` 及 `RELEASE_v*.md` 由 **Auto**（Cursor Agent）根据当前源码与发布包整理撰写，便于 GitHub 访客快速上手；技术细节以仓库内 `docs/` 为准。

---

## 功能概览

- **双模式桌面**：普通聊天（DeepSeek 网页）与 **Agent** 面板一键切换，双 WebView 保活、流畅显隐。
- **Harness**：本地智能体编排、工具审批、工作区文件/Shell、Skills 与多智能体工作流。
- **DSD API**：将已登录网页会话暴露为本地 OpenAI 兼容 HTTP（可选），供 Agent 或外部工具调用。
- **MCP**：可挂载 Model Context Protocol 服务器扩展工具面。
- **安全与合规**：见根目录 [DISCLAIMER.md](./DISCLAIMER.md)（非 DeepSeek 官方产品）。

---

## 快速安装（推荐）

无需编译，直接下载发布包：

1. 打开 [Releases](https://github.com/fanstars2318/deepseek-desktop/releases)，下载最新 **`DeepSeek-Desktop-v*-win-x64.zip`**。
2. 解压到任意目录（路径尽量不含特殊字符）。
3. 运行 **`DeepSeek.exe`**。
4. 若提示缺少运行时，请安装 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) 与 [WebView2 运行时](https://developer.microsoft.com/microsoft-edge/webview2/)。

详细步骤、目录说明与常见问题见 **[docs/INSTALL.md](./docs/INSTALL.md)**。

---

## 从源码构建

**环境**：Windows 10/11 x64、.NET 10 SDK、WebView2 Runtime。

```powershell
git clone https://github.com/fanstars2318/deepseek-desktop.git
cd deepseek-desktop
.\build.ps1
.\publish\DeepSeek.exe
```

发布 zip（本地打包，不上传 GitHub）：

```powershell
.\scripts\package-release.ps1 -Version 2.3.0
# 产物：.building/artifacts/release/DeepSeek-Desktop-2.3.0-win-x64.zip
```

提交前建议：

```powershell
.\scripts\audit-supply-chain.ps1
.\scripts\clean-repo-artifacts.ps1
.\build.ps1
```

---

## 仓库结构

| 路径 | 说明 |
|------|------|
| `Assets/` | 运行时 Web 资产（inject、agent、dsd-api） |
| `DeepSeek.Core/` | Harness、配置、DSD API 核心 |
| `Services/`、`Views/`、`MainWindow` | WPF 壳与 IPC |
| `scripts/` | 构建、验收、发布脚本 |
| `docs/` | 架构、Agent 指南、Interop 等 |
| `publish/` | **本地** `build.ps1` 输出（已 `.gitignore`，不进 Git） |

---

## 文档索引

| 文档 | 内容 |
|------|------|
| [docs/INSTALL.md](./docs/INSTALL.md) | 安装、升级、数据目录 |
| [docs/ARCHITECTURE.md](./docs/ARCHITECTURE.md) | 分层与发布门禁 |
| [docs/AGENT_USER_GUIDE.md](./docs/AGENT_USER_GUIDE.md) | Agent 使用与设置 |
| [docs/HARNESS.md](./docs/HARNESS.md) | Harness 行为说明 |
| [docs/MIGRATION.md](./docs/MIGRATION.md) | 与历史 `deepseek-edge` 的关系 |
| [DISCLAIMER.md](./DISCLAIMER.md) | 免责声明 |

---

## 贡献与版权

- **贡献者**：本仓库提交历史仅反映维护者 [@fanstars2318](https://github.com/fanstars2318) 的个人开发与发布。
- **许可**：以仓库内各文件及 `docs/THIRD_PARTY_LICENSES.md` 声明为准。
- **问题反馈**：请使用 [GitHub Issues](https://github.com/fanstars2318/deepseek-desktop/issues)。

---

## 与 deepseek-edge 的关系

`deepseek-edge` 为已归档的 Qt / Bridge 实验线，**不再**向本仓同步。日常开发、Issue 与 Release 均以 **本仓库（WPF 主线）** 为准。详见 [docs/MIGRATION.md](./docs/MIGRATION.md)。
