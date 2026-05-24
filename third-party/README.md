# third-party

## DeepSeek-TUI (`DeepSeek-TUI/`)

[DeepSeek-TUI](https://github.com/Hmbown/DeepSeek-TUI) 是桌面端 **Agent 引擎**（`deepseek serve --http` Runtime API）。本仓库以 **git submodule** 纳入，默认跟踪 **v0.8.39**。

### 首次克隆

```powershell
git clone --recurse-submodules <repo-url>
# 或已克隆后：
git submodule update --init --recursive
```

### 从源码构建 TUI 二进制

需要 **Rust 1.88+**（`rust-version = "1.88"`）：

```powershell
.\scripts\ensure-rust.ps1
.\build.ps1 -BuildTuiFromSource
# 等价于：
.\scripts\build-deepseek-tui.ps1 -TuiSourcePath .\third-party\DeepSeek-TUI
```

产物复制到 `publish/Assets/tools/`（`deepseek.exe` + `deepseek-tui.exe`）。

### 与桌面端关系

| 组件 | 角色 |
|------|------|
| WPF / WinUI 壳 | UI、网页桥、Chat2API |
| `DeepSeekTuiHost` | 子进程托管 `deepseek serve --http`（默认 :7878） |
| `DeepSeekTuiRuntimeClient` | HTTP Runtime API 客户端 |
| Agent LLM | TUI → 临时 loopback :17425 → 进程内 `bridge.js` → DeepSeek 网页会话 |

完整进程内 IPC 为后续工作；当前架构见 `DeepSeek.Core/Services/InternalChatChannel.cs`。

### 构建缓存

`third-party/DeepSeek-TUI/target/` 已在 `.gitignore` 中忽略，勿提交。
