# DeepSeek Desktop

基于 WebView2 的 DeepSeek 桌面客户端：普通网页对话 + Agent（Qwen Code Core C# 移植、MCP、Skills/Subagents）。

## 环境要求

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [WebView2 运行时](https://developer.microsoft.com/microsoft-edge/webview2/)

## 构建

```powershell
# 编译并复制到桌面 DeepSeek-Edge 文件夹
.\build.ps1

# 或仅发布到 publish/
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

## 配置

登录态与 MCP 配置保存在：

`%LocalAppData%\DeepSeekEdge\config.json`

## 仓库

https://github.com/fanstars2318/deepseek-desktop
