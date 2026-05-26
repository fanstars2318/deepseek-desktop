# LangGraph 式图工作流

对标 LangGraph：**声明式图 + thread checkpoint + interrupt/resume**。

## 图定义

放置于：

- `~/.deepseek/graphs/*.json|yaml`
- `<workspace>/.deepseek/graphs/`

示例（JSON）：

```json
{
  "id": "code-review",
  "nodes": [
    { "id": "explore", "type": "subagent", "role": "explore" },
    { "id": "implement", "type": "subagent", "role": "implementer" },
    { "id": "verify", "type": "tool", "tool": "bash", "args": { "command": "dotnet test" } },
    { "id": "human", "type": "interrupt", "prompt": "Approve merge?" }
  ],
  "edges": [
    { "from": "explore", "to": "implement" },
    { "from": "verify", "to": "human", "condition": "last_exit_code != 0" }
  ]
}
```

## 节点类型

| type | 行为 |
|------|------|
| `subagent` | `HarnessSubAgentService` + `role` |
| `tool` | `HarnessToolExecutor`（builtin/MCP） |
| `llm` | 单轮 completion |
| `interrupt` | 暂停；checkpoint 写入 `~/.deepseek/threads/{threadId}/` |

## 运行

- Strategy：`graph:<id>` 或 `/graph run <id>`
- Trace span：`graph.node`
- 恢复：`/resume thread <threadId>`

## 模块

`DeepSeek.Core/Services/Harness/Graph/` — `HarnessGraphDefinition`, `HarnessGraphRunner`, `HarnessGraphCheckpoint`, `HarnessGraphRegistry`
