# 从 deepseek-edge 迁移到 deepseek_desktop

1. **日常开发**在 `deepseek_desktop` 进行；`build.ps1` → `publish/DeepSeek.exe`。
2. **不要**从 `deepseek-edge\bin` 或旧 `DDpublish` 启动。
3. **同步**：仅在 edge 仓执行 `scripts/sync-to-deepseek-desktop.ps1`（排除 `bin/`、`obj/`、`publish/`）。
4. **edge 仓**已冻结为归档；新功能只提交 desktop 仓。
5. 远程 Git 请将 default 分支指向 `deepseek_desktop`（本地自行 `git remote`）。
