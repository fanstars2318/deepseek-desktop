# DD Qt 6 Hybrid Desktop Shell

DeepSeek Desktop（**DD**）可选用 **Qt 6 + QWebEngine** 作为主窗口，继续加载 `Assets/agent` 与 `Assets/inject` 中的现有 Web UI，实现与 WPF + WebView2 版本像素级一致的界面。

命名约定见 [DD_NAMING.md](./DD_NAMING.md)。

## 架构

| 进程 | 职责 |
|------|------|
| `DeepSeek.Qt.exe` | 主窗口：`QStackedWidget` 切换 Chat / Agent 两个 `QWebEngineView` |
| `DeepSeek.Bridge.exe` | 隐藏 WPF 子进程：`DesktopAgentHost` + 隐藏 WebView2（Chat API / `WebChatBridgeHost`） |
| 命名管道 `dd-desktop-bridge` | Qt ↔ Bridge 换行分隔 JSON，`{ channel, payload }` |

虚拟域名（与 WebView2 一致）：

- `https://ds-agent.local/` → `Assets/agent/`
- `https://ds-inject.local/` → `Assets/inject/`
- `https://ds-chat2api.local/` → `Assets/chat2api/`

## 环境要求

- Windows 10/11 x64
- **Qt 6.6+** MSVC 64-bit kit，含 **WebEngine** 与 **WebChannel** 模块
- CMake 3.21+、Ninja（推荐）
- .NET 10 SDK（Bridge 与 Harness）
- WebView2 Runtime（Bridge 子进程仍需要）

设置 kit 路径（示例）：

```powershell
$env:CMAKE_PREFIX_PATH = "C:\Qt\6.8.0\msvc2022_64"
```

## 构建

```powershell
# 检测到 Qt MSVC 工具链时会自动走 Hybrid（可用 -NoAutoQt 强制 WPF）
.\build.ps1

# 显式 Qt 发布（未安装 Qt 时仍会发布 Bridge + Assets，并跑 IPC 自检）
.\build.ps1 -Qt
```

管道回声自检（无需 Qt GUI）：

```powershell
dotnet publish DeepSeek.DdBridge\DeepSeek.DdBridge.csproj -c Release -r win-x64 -o publish
.\scripts\verify-dd-ipc.ps1 -PublishDir publish
```

产物在 `publish/`：

- `DeepSeek.exe` — 启动器（若存在 `DeepSeek.Qt.exe` 则启动 Qt 壳）
- `DeepSeek.Qt.exe` — Qt 主程序（含 `windeployqt` 部署的运行时）
- `DeepSeek.Bridge.exe` — C# 后端子进程
- `Assets/**` — Web 资源

仅编译 C# Bridge（不构建 Qt）：

```powershell
dotnet publish DeepSeek.DdBridge\DeepSeek.DdBridge.csproj -c Release -r win-x64 -o publish
```

## 调试

- **Agent / Chat 页 DevTools**：在 `MainWindow` 中为对应 `QWebEngineView` 调用 `page()->setDevToolsPage(new QWebEnginePage(profile))` 并 `devToolsPage()->show()`（可自行临时添加）。
- **Bridge 日志**：`%LOCALAPPDATA%\DeepSeek\logs\`
- **管道协议**：`channel` 为 `agent` | `chat` | `control`；`control` + `ddReady` / `ddSurface`

## JS 桥接

`Assets/inject/dd-webview-shim.js` 在 DocumentCreation 注入，将 `window.chrome.webview.postMessage` 映射到 `QWebChannel` 的 `deepSeekHost.receiveMessage`；宿主回推仍走 `window.dsDesktopOnMessage`（与 WebView2 相同）。

构建时会尝试从 Qt 安装目录复制 `qwebchannel.js` 到 `Assets/inject/`（`scripts/build-qt.ps1`）。

## 与 WPF 默认构建的区别

| 项目 | WPF（默认） | Qt（`-Qt`） |
|------|-------------|-------------|
| 主程序 | `DeepSeek.App.exe` | `DeepSeek.Qt.exe` |
| Agent 渲染 | WebView2 | QWebEngine |
| `DesktopAgentHost` | 主进程内 | `DeepSeek.Bridge.exe` |
