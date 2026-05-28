# DeepSeek Desktop v2.2.0

> Release 说明撰写：**Auto**（Cursor Agent）  
> 发布维护：**[@guyu23223](https://github.com/guyu23223)**

---

## 概述

**v2.2.0** 为 **WPF + WebView2 主线**的 Windows x64 便携发布，与仓库当前 `main` 分支一致（提交 `296d847` 及之后文档更新）。相比 v2.1.0 的 Qt Hybrid 实验包，本版本仅包含 **`DeepSeek.exe` 单入口**，不再附带 `DeepSeek.Qt.exe` / `DeepSeek.Bridge.exe`。

---

## 亮点

- **WPF 单壳**：`build.ps1` 唯一发布路径，结构精简、启动路径清晰。
- **双 WebView 工作模式**：聊天页与 Agent 页保活切换，注入与加载策略已按桌面场景优化（见 `docs/ARCHITECTURE.md`）。
- **Harness + DSD API + MCP**：保留 v2.0 起的 Agent 编排、本地 OpenAI 兼容层与 MCP 扩展能力。
- **文档整理**：README、`docs/INSTALL.md` 与 Release 说明面向 GitHub 访客重写，便于下载即用。

---

## 下载与安装

| 文件 | 说明 |
|------|------|
| `DeepSeek-Desktop-v2.2.0-win-x64.zip` | 完整便携目录（约 26 MB 压缩前体量因版本而异） |

**步骤：**

1. 下载 zip 并解压。
2. 运行 `DeepSeek.exe`。
3. 安装 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) 与 [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)（若系统尚未安装）。

详细说明：[docs/INSTALL.md](./docs/INSTALL.md)

---

## 升级自 v2.1.0 / v2.0.0

| 项目 | 说明 |
|------|------|
| 配置与数据 | 仍位于 `%LocalAppData%\deepseek_desktop\` 与 `~/.deepseek/` |
| Qt 壳 | v2.2.0 **不包含** Qt Hybrid；若你依赖 `DeepSeek.Qt.exe`，请继续使用 [v2.1.0 资产](https://github.com/guyu23223/deepseek-desktop/releases/tag/v2.1.0) 或自行 `-Qt` 构建（实验性，见 `docs/DD_DESKTOP.md`） |
| 命名与 IPC | DD 统一命名规范不变，见 [docs/DD_NAMING.md](./docs/DD_NAMING.md) |

---

## 从源码构建

```powershell
git clone https://github.com/guyu23223/deepseek-desktop.git
cd deepseek-desktop
git checkout v2.2.0
.\build.ps1
.\publish\DeepSeek.exe
```

---

## 校验

- 主程序文件版本信息中的 **ProductVersion** 应包含对应 Git 提交短哈希（例如 `1.0.0+296d847...`）。
- 发布包内应包含 `Assets\inject\bridge.js`、`Assets\agent\index.html` 等 Web 资源。

---

## 免责声明

本软件为社区维护的**非官方**工具。使用前请阅读 [DISCLAIMER.md](./DISCLAIMER.md)。
