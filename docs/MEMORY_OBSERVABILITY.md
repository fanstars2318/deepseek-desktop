# 记忆与可观测性（本地 Mem0 / Langfuse 子集）

DSD Harness 在保留 L0–L3 YAML 与 `AgentDebugLogger` 的前提下，叠加：

| 能力 | 实现 | 路径 |
|------|------|------|
| **Run Trace** | `HarnessRunTracer` | `<workspace>/.deepseek/runs/{runId}/trace.jsonl` + `meta.json` |
| **语义记忆** | `HarnessSemanticMemoryStore` (SQLite) | `~/.deepseek/memory/semantic.db` |
| **Embedding** | `HarnessEmbeddingClient` | 同 Agent API `/v1/embeddings` |

## 斜杠命令

- `/runs` — 最近 Run 列表（token、耗时）
- `/trace <runId>` — span 时间线
- `/memory [query]` — 搜索语义记忆

## 配置（AppConfig / 设置页）

- `AgentStructuredTraceEnabled`（默认 true）
- `AgentTraceRetentionDays`（默认 30）
- `AgentSemanticMemoryEnabled`（默认 true）
- `AgentSemanticMemoryAutoExtract`（默认 false）
- `AgentSemanticMemoryTopK` / `AgentSemanticMemoryMaxChars`
- `AgentEmbeddingModel`

## Prompt 注入顺序

1. Semantic top-K（若启用）
2. Checkpoint summary
3. L2 / L3 / L1 / L0 YAML

## 参考仓库（只读）

本地参考：`C:\Users\xiaow\Desktop\DSD\{mem0-main,langfuse-main,opik-main}` — `.\scripts\setup-reference-repos.ps1` 仅校验路径（`-TryClone` 可选补克隆）。

## Eval

- 数据集：`~/.deepseek/evals/*.json`
- 脚本：`scripts/agent-eval.ps1`（scaffold，可扩展 IPC 批量跑）
