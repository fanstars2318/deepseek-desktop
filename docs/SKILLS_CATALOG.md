# Skill 目录索引

## 扫描路径

见 [INTEROP.md](./INTEROP.md)。额外根目录：

```json
"AgentSkillExtraRoots": [
  "C:\\Users\\xiaow\\Desktop\\DSD\\antigravity-awesome-skills-main",
  "C:\\Users\\xiaow\\Desktop\\DSD\\awesome-claude-skills-master"
]
```

## 索引

- `HarnessSkillCatalogIndexer.Reindex` → `~/.deepseek/skills/index.json`
- 跳过无 description 的 SKILL.md

## 命令

- `/skills reindex`
- `/skills search <query>`
- `/skill <id>` — 手动启用（可选）

## 本地合集（已解压 *-main）

```powershell
.\scripts\setup-skill-catalog.ps1
```

写入 `~/.deepseek/config.json` 并提示 `/skills reindex`。新安装默认路径已写入 `AppConfig.AgentSkillExtraRoots`。
