# deepseek-edge 优化路线图

基准仓库：`C:\Users\xiaow\Desktop\DSD\deepseek-edge`（主开发）  
参考源码：DSD 下 `*-main` / `*-master` 解压目录（只读对照，不引入 Python 运行时）。

## 已落地（基线）

| 能力 | 参考 | DSD 实现 |
|------|------|----------|
| OpenAI tools + 沙盒 | deepcode-cli | `HarnessReadFileTool`、权限 scope、checkpoint |
| 多 Agent SOP | MetaGPT | `HarnessTeamOrchestrator` |
| 并行扇出 | AutoGen | `HarnessParallelExploreOrchestrator` |
| 双 Agent 辩论 | CAMEL | `HarnessDebateOrchestrator` |
| 图工作流 | LangGraph | `HarnessGraphRunner` |
| 语义记忆 + Trace | Mem0 / Langfuse 子集 | `HarnessSemanticMemoryStore`、`HarnessRunTracer` |
| 自动意图路由 | — | `HarnessRunIntentPlanner` |
| Token 预算 | — | `HarnessPromptBudget`、压缩阈值 40k |
| 设置 UI | — | Tab 化 `settings-embed` + `DesktopSettingsWindow` |

## 用户确认（2026-05-25）

- **开发根**：先统一 Desktop ↔ worktree，后续以 Desktop 为准
- **Skill 根**：`antigravity-awesome-skills-main` + `awesome-claude-skills-master`
- **意图规划**：默认 **LLM 规划**（寒暄仍跳过）
- **可观测**：**Langfuse 深度集成**（非仅本地 JSONL）
- **阶段**：P0–P6 全部纳入

## 执行计划

| 阶段 | 目标 | 关键交付 | 参考源码 | 状态 |
|------|------|----------|----------|------|
| **P0 接线** | 路径、默认配置、脚本与文档一致 | `setup-skill-catalog.ps1`、`setup-reference-repos.ps1`、`AppConfig` 默认 Skill 根、LLM 意图默认 | 本地 *-main | 🔄 进行中 |
| **P1 Token** | 同任务更少 token | 动态工具子集、意图缓存、MCP 按任务过滤、压缩策略可配置 | langchain tool binding | 待办 |
| **P2 Langfuse** | 全链路可观测 | `HarnessLangfuseExporter`、设置页 Host/Key/Project、span 映射、失败降级本地 | `langfuse-main` | 待办 |
| **P3 记忆** | Mem0 式增强 | 去重合并、session/user 分层、可选 auto-extract 策略 UI | `mem0-main` | 待办 |
| **P4 多智能体** | 更智能协作 | AutoGen 动态选讲者、MetaGPT 可配置 SOP YAML、子 Agent 结果压缩 | autogen / MetaGPT | 待办 |
| **P5 图工作流** | 生产可用图 | `parallel`/`map` 节点、子图、更多示例、Agent `/graph list` UI | langchain-master graph | 待办 |
| **P6 技能生态** | 大目录可用 | 懒加载索引、按意图只注入 Top-N Skill、reindex 进度 UI | antigravity / awesome-claude | 待办 |

## P2 Langfuse 技术草案

1. 配置：`AgentLangfuseEnabled`、`AgentLangfuseHost`、`AgentLangfusePublicKey`、`AgentLangfuseSecretKey`、`AgentLangfuseProject`
2. `HarnessRunTracer` 结束时批量 POST ingestion API（对齐 OpenTelemetry-like span）
3. 设置页「可观测性」Tab：测试连接、最近 Run 外链（若 Langfuse UI 可拼 URL）
4. 离线时仅写本地 `trace.jsonl`，不阻塞 Run

## P6 技能索引策略

1. 首次 `/skills reindex` 只写 `id/name/description/path`（不读全文）
2. `HarnessRunIntentPlanner` 仅对 Top-6 候选 `Load` 正文
3. 索引 &gt; 5000 条时后台增量更新（避免 UI 卡顿）

## 风险与权衡

- **LLM 意图默开** vs **P1 压 Token**：通过 P1 工具子集与短规划 prompt 对冲
- **Langfuse 全量**：需公网或自建实例；密钥仅存 `~/.deepseek`
- **800+ Skill**：必须懒加载，否则索引与启动变慢

---

*实施顺序：P0 → P1 → P2 → P3 → P4 → P5 → P6；每阶段完成后 `dotnet test DeepSeek.Core.Tests`。*
