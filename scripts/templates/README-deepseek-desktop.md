# DeepSeek Desktop（deepseek_desktop）

本目录为 **WPF 主线** 的瘦身工作区：仅含可交付源码与资产，不含 Qt / WinUI / Bridge 实验代码。

历史全量仓库（含实验壳）仍保留在同级目录 `deepseek-edge`，本仓库不再向其同步外围实验子项目。

## 目录约定

| 路径 | 说明 |
|------|------|
| `publish/` | 唯一编译发布输出（运行 `.\build.ps1` 生成） |
| `Assets/` | 运行时 Web 资产（inject、agent、chat2api） |
| `DeepSeek.Core/` | 共享业务与 Harness |
| `scripts/` | 构建、验收、供应链审计 |

## 快速开始

```powershell
.\build.ps1
.\publish\DeepSeek.exe
```

## 与 deepseek-edge 的关系

- **从此目录开发**：日常改 WPF + Agent + 注入脚本。
- **重新同步上游**：在 `deepseek-edge` 中改完后执行  
  `.\scripts\sync-to-deepseek-desktop.ps1 -RunBuild`
- **不要**把 `bin/`、`obj/`、`publish/` 提交进 Git。

## 工程验收

见 `docs/ENGINEERING_REVIEW.md`、`docs/PERIPHERAL_AUDIT.md`。提交 PR 前请运行：

```powershell
.\scripts\audit-supply-chain.ps1
.\scripts\clean-repo-artifacts.ps1
.\build.ps1
```
