# Agent 应用模式对照（awesome-llm-apps）

参考 `C:\Users\xiaow\Desktop\DSD\awesome-llm-apps`（只读），映射到 DSD 能力：

| 样例模式 | DSD 实现 |
|----------|----------|
| 单 Agent ReAct | `strategy=execute` + `HarnessOrchestrator` |
| 多 Agent 路由 | `/team` · `/parallel-explore` · `/debate` · `delegate_agent` |
| AutoGen 群聊扇出 | `parallel_explore` · `HarnessParallelExploreOrchestrator` |
| CAMEL 辩论 | `HarnessDebateOrchestrator` · advocate/critic |
| 声明式工作流 | `strategy=graph:<id>` · `~/.deepseek/graphs/` |
| RAG / 记忆 | L0–L3 YAML + `HarnessSemanticMemoryStore` |
| Human-in-the-loop | Graph `interrupt` + `/resume thread` |
| 定时/触发 | `AgentAutomation` + Webhook |
| 可复用模块 | `HarnessBlockRegistry` + Playbook `blocks:` |
| 可观测性 | `HarnessRunTracer` · `/runs` `/trace` |

不为每个 upstream 样例移植代码；用 Playbook / Graph / Team 模板组合。
