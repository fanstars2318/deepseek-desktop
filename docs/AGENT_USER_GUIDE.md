# DeepSeek Edge Agent 使用指南

本文档面向日常使用 DeepSeek Edge 桌面版内嵌 Agent（DSD Harness）的用户，涵盖设置、命令、Token 优化与多智能体能力。

> 在 Agent 设置页点击 **说明文档**，或在仓库中打开 `docs/AGENT_USER_GUIDE.md`。

---

## 1. 快速开始

1. **登录网页会话**：在 DeepSeek 网页完成登录，Token 会自动同步到本地 Chat2API。
2. **打开 Agent**：使用桌面侧栏 Agent 面板，直接描述任务（无需手动指定工具名）。
3. **打开设置**：Agent 内 **设置** 按钮，或 `~/.deepseek/config.json`。
4. **运行 doctor**：设置 → 概览 → **运行 doctor**，检查 Harness、MCP、工作区状态。

推荐默认工作流：**Execute**（直接执行）。复杂需求可选用 Blueprint、Team、并行 Explore 或 Debate。

---

## 2. 设置页说明（DeepSeek 风格 UI）

设置分为六个标签，与 DeepSeek 蓝白卡片风格一致。

### 2.1 概览

| 项 | 说明 |
|---|---|
| 网页会话 | 是否已登录 DeepSeek |
| Agent 引擎 | Harness 是否就绪 |
| 能力标签 | 根据当前配置显示已启用功能（智能意图、Token 精简、MCP 等） |
| 说明文档 | 打开本指南 |
| doctor | 环境自检 |

### 2.2 Agent

- **默认工作流**：Execute / Blueprint / Team / Parallel Explore / Debate
- **最大步数**：主 Agent 与子 Agent 步数上限
- **工作区根目录**：文件与 Shell 工具的作用范围
- **工具审批**：smart（只读自动）/ readonly / always / never
- **允许 Shell**、**沙盒懒加载**、**输出截断续写**

### 2.3 多智能体

**智能意图（推荐开启）**

| 开关 | 默认 | 说明 |
|---|---|---|
| 自动分析 Skill/工具 | 开 | Run 前启发式匹配 Skill 与 MCP |
| 模型生成意图规划 | **关** | 额外 1 次 LLM 调用；复杂任务再开 |
| 同会话复用意图 | 开 | 相同 prompt 后续轮跳过规划 |
| 精简系统提示 | 开 | 有意图时跳过 MCP 长目录，显著省 Token |
| OpenAI MCP 工具上限 | 8 | API/OpenAI tools 模式下 schema 裁剪 |
| MCP 目录行数 | 16 | XML 模式下系统提示中的 MCP 列表 |
| 上下文压缩 | 40K | 超过阈值触发历史压缩；0=关闭 |
| 工具输出内联上限 | 3000 字符 | 超出可落盘到 `.deepseek/runs/` |
| Skill 注入字符 | 3000 | Skill 正文截断上限 |
| 工作区快照条目 | 30 | 系统提示中目录快照条数 |

**协作模式**：子 Agent、Team 梦之队、并行 Explore、**动态选讲者**、CAMEL 辩论及并发/轮数。

### 2.4 记忆

- **Langfuse Cloud**：Run 结束后导出 trace（需 Public/Secret Key）
- **Run Trace**：本地 `~/.deepseek/runs/<runId>/trace.jsonl`
- **语义记忆**：SQLite + embedding；Top-K 注入；**Session TTL**（默认 7 天）

### 2.5 MCP

- **连接全部** / 添加 / 编辑 / 禁用
- 配置文件路径可复制、直接打开

### 2.6 高级

- **推理通道**：网页桥接 / 直连 API
- **工具协议**：XML / OpenAI function calling（API 模式自动 OpenAI）
- API Key、Vision、notify 脚本等

---

## 3. 聊天命令

在 Agent 输入框以 `/` 开头：

| 命令 | 说明 |
|---|---|
| `/runs` | 列出最近 Run；含 Langfuse 链接（若已配置） |
| `/runs <id>` | 查看单次 Run 摘要 |
| `/skills` | 已加载 Skill 列表 |
| `/skills reindex` | 后台重建 Skill 索引（大量 Skill 时） |
| `/memory` | 语义记忆统计 |
| `/memory clear session` | 清理当前会话 scope 记忆 |
| `/graph list` | 已注册图工作流 |
| `/graph run <name>` | 运行 JSON 图（见 `docs/examples/graphs/`） |
| `/team` | MetaGPT 式梦之队 SOP |
| `/parallel-explore` | 多路只读调研 |
| `/debate` | Advocate ↔ Critic 辩论 |

Team SOP 自定义：在 `~/.deepseek/team-sops/` 放置 YAML，见 `docs/MULTI_AGENT.md`。

---

## 4. Token 优化策略（推荐配置）

默认已按「省 Token、保能力」调优：

1. **关闭 LLM 意图规划**，保留启发式路由 + **意图缓存**。
2. **开启精简系统提示**：计划工具在意图段列出，不再重复整份 MCP 目录。
3. **OpenAI 模式**：`HarnessToolFilter` 按意图裁剪 MCP schema，上限 8 个。
4. **XML 模式**：MCP 目录最多 16 行；工具输出超 3000 字符落盘摘要。
5. **寒暄检测**：简单问候跳过意图规划与工具注入。
6. **上下文压缩**：长对话超过 40K token 阈值时压缩历史。

若任务复杂、工具匹配不准，可临时开启「模型生成意图规划」，或增大 MCP 工具上限。

---

## 5. 多智能体与工作流

| 模式 | 适用场景 |
|---|---|
| Execute | 改代码、跑命令、日常任务 |
| Blueprint | 先 Explore 再出方案 |
| Team | 需求 → 架构 → 实现 → 评审 |
| Parallel Explore | 多路径只读调研后汇总 |
| Debate | 方案对立辩论 |
| Graph | JSON 定义 parallel/map/subgraph 节点 |

详见：`docs/MULTI_AGENT.md`、`docs/GRAPH_WORKFLOW.md`。

---

## 6. 记忆与可观测性

- **本地 Trace**：`AgentStructuredTraceEnabled=true`（默认）
- **Langfuse**：设置页填写 `https://cloud.langfuse.com` 与 Keys
- **语义记忆**：Run 前 Top-K 检索；可选任务结束自动提取
- **Session TTL**：`AgentSemanticMemorySessionTtlDays=7` 自动清理 session scope

详见：`docs/MEMORY_OBSERVABILITY.md`。

---

## 7. Skill 目录

默认扫描：

- `C:\Users\xiaow\Desktop\DSD\antigravity-awesome-skills-main`
- `C:\Users\xiaow\Desktop\DSD\awesome-claude-skills-master`

安装/更新：运行 `scripts/setup-skill-catalog.ps1`，Agent 内 `/skills reindex`。

详见：`docs/SKILLS_CATALOG.md`。

---

## 8. 自动化验收

开发或发版前在仓库根目录执行：

```powershell
.\scripts\agent-acceptance.ps1
```

仅 Token 相关测试：

```powershell
.\scripts\agent-acceptance.ps1 -TokenOnly
```

包含：`HarnessTokenOptimizationTests`、`HarnessToolFilterTests` 及全量 130+ 单元测试。

---

## 9. 目录与配置

| 路径 | 内容 |
|---|---|
| `~/.deepseek/config.json` | 主配置（与设置页同步） |
| `~/.deepseek/runs/` | Run trace、postmortem、工具输出 spill |
| `~/.deepseek/memory/` | 语义记忆 SQLite |
| `~/.deepseek/team-sops/` | Team YAML |
| `docs/examples/graphs/` | 图工作流示例 |

---

## 10. 故障排查

| 现象 | 处理 |
|---|---|
| Agent 未就绪 | 确认网页已登录；运行 doctor |
| MCP 工具不可用 | 设置 → MCP → 连接全部 |
| Token 消耗高 | 确认「精简系统提示」开、LLM 意图关；减小 MCP 上限 |
| Skill 未匹配 | `/skills reindex`；检查 `AgentSkillExtraRoots` |
| Langfuse 无数据 | 检查 Keys、Project；需 Run 正常结束 |

更多架构细节见 `docs/HARNESS.md`。

---

*文档版本：与 P0–P6 优化路线图及 Token/UI 验收同步。*
