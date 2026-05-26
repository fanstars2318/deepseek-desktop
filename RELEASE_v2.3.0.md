# DeepSeek Desktop v2.3.0

> Release 说明撰写：**Auto**（Cursor Agent）  
> 发布维护：**[@fanstars2318](https://github.com/fanstars2318)**

---

## 概述

**v2.3.0** 对应当前 `main` 分支与本地编译产物 **`DDpublish`**（WPF + WebView2，Windows x64 便携包）。本版本在 v2.2.0 基础上完成 **Chat2API → DSD API** 架构迁移，并强化 Agent 设置、API 管理与 Harness 编排能力。

---

## 亮点

- **DSD API 统一命名**：本地 OpenAI 兼容层与设置/UI 统一为 **DSD API**（`DeepSeek.Core/Services/ApiManagement`、`DsdOpenAiCompat` 等）。
- **API 管理**：多提供商注册、路由解析、OAuth 回调与 Sidecar 适配器；Agent 通过 `provider-accounts.json` 管理 API 账户。
- **Agent 设置嵌入页**：`Assets/agent/settings-embed` 与 `dsd-api` 模块整合，支持账户与模型配置。
- **Harness 增强**：循环内核、子 Agent 结果压缩、BM25 记忆检索、Worker 进程池、软件工厂编排等。
- **桌面壳精简**：移除独立 Chat2API 控制台窗口；IPC 改为 `DsdApiIpcBridge` / `DsdApiStackBootstrap`。
- **构建脚本**：`build-dsd-api-ui.ps1`、`sync-agent-dsd-api.ps1`；`build.ps1` 仍为唯一发布入口。

---

## 下载与安装

| 文件 | 说明 |
|------|------|
| `DeepSeek-Desktop-v2.3.0-win-x64.zip` | 完整便携目录（由 `DDpublish` 打包，约 26 MB 解压前） |

**步骤：**

1. 下载 zip 并解压。
2. 运行 `DeepSeek.exe`。
3. 安装 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) 与 [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)（若尚未安装）。

详细说明：[docs/INSTALL.md](./docs/INSTALL.md)

---

## 升级自 v2.2.0

| 项目 | 说明 |
|------|------|
| 配置与数据 | 仍在 `%LocalAppData%\deepseek_desktop\` 与 `~/.deepseek/` |
| Chat2API 命名 | 文档与 UI 已改为 **DSD API**；运行时 `Assets` 中可能仍保留 `chat2api` 目录名（兼容发布路径） |
| 构建产物 | 请使用本 Release 的 zip 或自行 `.\build.ps1`，勿混用旧版 `DDpublish` |

---

## 从源码构建

```powershell
git clone https://github.com/fanstars2318/deepseek-desktop.git
cd deepseek-desktop
git checkout v2.3.0
.\build.ps1
.\publish\DeepSeek.exe
```

---

## 免责声明

本软件为社区维护的**非官方**工具。使用前请阅读 [DISCLAIMER.md](./DISCLAIMER.md)。
