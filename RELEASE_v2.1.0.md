# DeepSeek Desktop (DD) v2.1.0

## 亮点

- **DD 统一命名**：桌面壳、IPC、脚本与文档统一为 DeepSeek Desktop (DD)；接口 `IDdWebPages` / `IDdPageMessenger`，管道 `dd-desktop-bridge`
- **Qt Hybrid 可选壳**：`DeepSeek.Qt.exe` + `DeepSeek.Bridge.exe`（WPF 子进程）通过命名管道驱动 Agent/Chat；默认发布仍为 WPF + WebView2
- **DdBridge IPC**：`ddReady` / `ddSurface` 控制消息；`verify-dd-ipc.ps1` 自动化验证
- **结构精简**：移除旧 `DeepSeek.QtBridge` / `Services/Qt` 副本；WPF 构建同步打包 `DeepSeek.Bridge.exe`
- **性能小步优化**：减少重复 `ConfigStore.Load()`、workMode 重试轮次、登录轮询 IPC；Chat 注入脚本合并

## 安装

1. 下载 `DeepSeek-Desktop-v2.1.0-win-x64.zip`
2. 解压到任意目录
3. 运行 `DeepSeek.exe`（Launcher 启动 `DeepSeek.App.exe`）
4. 需已安装 [WebView2 运行时](https://developer.microsoft.com/microsoft-edge/webview2/)

## 构建

```powershell
# 默认 WPF 发布（含 DdBridge + verify-dd-ipc）
.\build.ps1 -LegacyWpf -NoAutoQt

# 可选 Qt 6 Hybrid 主壳（需 Qt MSVC WebEngine kit）
.\build.ps1 -Qt
```

## 升级说明

- v2.1.0 保留 v2.0 全部 Harness / Automations / 沙盒能力
- 配置与数据仍在 `%LocalAppData%\deepseek_desktop\`
- 命名规范见 [docs/DD_NAMING.md](./docs/DD_NAMING.md)；Qt Hybrid 见 [docs/DD_DESKTOP.md](./docs/DD_DESKTOP.md)
