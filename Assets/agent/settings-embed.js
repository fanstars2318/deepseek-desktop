(function () {
  "use strict";

  const pending = new Map();
  let seq = 0;

  function isInAgentIframe() {
    try {
      return window.parent !== window;
    } catch (_) {
      return false;
    }
  }

  function post(type, payload) {
    const body = { type, __dsEmbed: true, ...(payload || {}) };
    const json = JSON.stringify(body);
    if (isInAgentIframe()) {
      window.parent.postMessage(json, "*");
      return;
    }
    if (window.chrome?.webview) {
      window.chrome.webview.postMessage(json);
      return;
    }
  }

  function postAsync(type, payload) {
    return new Promise((resolve, reject) => {
      const reqId = "s" + ++seq;
      const timer = setTimeout(() => {
        pending.delete(reqId);
        reject(new Error("请求超时"));
      }, 30000);
      pending.set(reqId, (msg) => {
        clearTimeout(timer);
        if (msg.ok === false) reject(new Error(msg.error || "操作失败"));
        else resolve(msg);
      });
      post(type, { ...(payload || {}), reqId });
    });
  }

    function onHostMessage(raw) {
    let msg = raw;
    if (typeof raw === "string") {
      try { msg = JSON.parse(raw); } catch (_) { return; }
    }
    if (!msg?.type) return;
    if (msg.type === "settingsBootstrap") {
      applyPayload({ loggedIn: !!msg.loggedIn });
      return;
    }
    if (msg.reqId && pending.has(msg.reqId)) {
      pending.get(msg.reqId)(msg);
      pending.delete(msg.reqId);
    }
  }

  function bindHostMessages() {
    if (isInAgentIframe()) {
      window.addEventListener("message", (e) => {
        if (e.source !== window.parent) return;
        onHostMessage(e.data);
      });
      return;
    }
    if (window.chrome?.webview) {
      window.chrome.webview.addEventListener("message", (e) => onHostMessage(e.data));
      return;
    }
    setTimeout(bindHostMessages, 20);
  }

  function $(id) { return document.getElementById(id); }

  function renderMcpList(servers) {
    const list = $("mcp-list");
    if (!list) return;
    list.innerHTML = "";
    if (!servers?.length) {
      list.innerHTML = "<p class=\"se-hint\">尚未添加 MCP 服务器。点击下方「添加 MCP 服务器」，连接成功后 Agent 可动态发现并调用工具。</p>";
      return;
    }
    servers.forEach((s) => {
      const item = document.createElement("div");
      item.className = "se-mcp-item";
      const status = !s.enabled
        ? "已禁用"
        : s.connected
          ? `已连接 · ${s.toolCount || 0} 个工具可用`
          : "未连接 — 请启用或点击「连接全部」";
      item.innerHTML =
        `<h4>${escapeHtml(s.name || "MCP Server")}</h4>` +
        `<div class="se-mcp-meta">${escapeHtml(s.transport || "")} · ${escapeHtml(s.endpoint || "")}</div>` +
        `<div class="se-mcp-meta">${escapeHtml(status)}</div>` +
        `<div class="se-mcp-actions">` +
        `<button type="button" class="se-btn" data-action="toggle" data-id="${escapeAttr(s.id)}">${s.enabled ? "禁用" : "启用"}</button>` +
        `<button type="button" class="se-btn" data-action="edit" data-id="${escapeAttr(s.id)}">编辑</button>` +
        `<button type="button" class="se-btn" data-action="remove" data-id="${escapeAttr(s.id)}">删除</button>` +
        `</div>`;
      list.appendChild(item);
    });
    list.querySelectorAll("button[data-action]").forEach((btn) => {
      btn.addEventListener("click", () => handleMcpAction(btn.dataset.action, btn.dataset.id));
    });
  }

  function escapeHtml(text) {
    return String(text || "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function escapeAttr(text) {
    return escapeHtml(text).replace(/'/g, "&#39;");
  }

  function renderFeaturePills(data) {
    const root = $("feature-pills");
    if (!root) return;
    const pills = [];
    if (data.agentAutoIntentRouting !== false) pills.push("智能意图");
    if (data.agentPromptMinimalMode !== false) pills.push("Token 精简");
    if (data.agentIntentCacheEnabled !== false) pills.push("意图缓存");
    if (data.enableSubAgents !== false) pills.push("子 Agent");
    if (data.enableTeamWorkflow !== false) pills.push("Team SOP");
    if (data.enableParallelExplore !== false) pills.push("并行 Explore");
    if (data.enableDynamicGroupChat) pills.push("动态选讲者");
    if (data.agentSemanticMemoryEnabled !== false) pills.push("语义记忆");
    if (data.agentStructuredTraceEnabled !== false) pills.push("Run Trace");
    if (data.agentLangfuseEnabled) pills.push("Langfuse");
    if ((data.mcpServers || []).some((s) => s.enabled && s.connected)) pills.push("MCP 已连接");
    root.innerHTML = pills.length
      ? pills.map((p) => `<span class="se-pill">${escapeHtml(p)}</span>`).join("")
      : "<span class=\"se-hint se-hint-flush\">保存设置后此处显示已启用能力</span>";
  }

  function applyPayload(data) {
    const tokenEl = $("web-token-status");
    if (tokenEl) {
      tokenEl.textContent = data.loggedIn ? "已登录" : "未登录";
      tokenEl.className = "se-stat-value " + (data.loggedIn ? "se-status-ok" : "se-status-bad");
    }
    const engineEl = $("engine-status");
    if (engineEl) {
      engineEl.textContent = data.loggedIn ? "就绪" : "需登录";
      engineEl.className = "se-stat-value " + (data.loggedIn ? "se-status-ok" : "se-status-bad");
    }
    const summaryEl = $("service-summary");
    if (summaryEl) {
      summaryEl.textContent =
        data.serviceSummary || "对话桥接与进程内 Harness 在后台运行。";
    }
    $("max-steps").value = data.maxAgentSteps ?? 30;
    $("max-sub-steps").value = data.maxSubAgentSteps ?? 10;
    const subOn = $("enable-sub-agents");
    if (subOn) subOn.checked = data.enableSubAgents !== false;
    const teamOn = $("enable-team");
    if (teamOn) teamOn.checked = data.enableTeamWorkflow !== false;
    const parOn = $("enable-parallel-explore");
    if (parOn) parOn.checked = data.enableParallelExplore !== false;
    const debOn = $("enable-debate");
    if (debOn) debOn.checked = data.enableDebateWorkflow !== false;
    const conc = $("max-concurrent-sub");
    if (conc) conc.value = data.maxConcurrentSubAgents ?? 3;
    const fan = $("parallel-fan-out");
    if (fan) fan.value = data.parallelExploreFanOut ?? 3;
    const debR = $("debate-rounds");
    if (debR) debR.value = data.debateMaxRounds ?? 3;
    const infEl = $("inference-mode");
    if (infEl) infEl.value = data.agentInferenceMode || "web";
    const protoEl = $("tool-protocol");
    if (protoEl) protoEl.value = data.agentToolCallingProtocol || "xml";
    const effortEl = $("reasoning-effort");
    if (effortEl) effortEl.value = data.agentReasoningEffort || "max";
    const thinkEl = $("thinking-display");
    if (thinkEl) thinkEl.value = data.agentThinkingDisplayMode || "normal";
    const apiBaseEl = $("api-base");
    if (apiBaseEl) apiBaseEl.value = data.agentApiBaseUrl || "";
    const apiKeyEl = $("api-key");
    if (apiKeyEl) apiKeyEl.value = data.agentApiKey || "";
    const visionModelEl = $("vision-model");
    if (visionModelEl) visionModelEl.value = data.agentVisionModel || "";
    const visionBaseEl = $("vision-base");
    if (visionBaseEl) visionBaseEl.value = data.agentVisionApiBaseUrl || "";
    const visionKeyEl = $("vision-key");
    if (visionKeyEl) visionKeyEl.value = data.agentVisionApiKey || "";
    const notifyEl = $("notify-script");
    if (notifyEl) notifyEl.value = data.agentNotifyScript || "";
    const strat = (data.defaultAgentStrategy || "execute").toLowerCase();
    const stratSel = $("strategy");
    if (stratSel) {
      if (strat === "team" || strat === "sop") stratSel.value = "team";
      else if (strat === "parallel-explore" || strat === "parallel_explore") stratSel.value = "parallel-explore";
      else if (strat === "debate" || strat === "camel") stratSel.value = "debate";
      else if (strat === "plan" || strat === "blueprint") stratSel.value = "blueprint";
      else stratSel.value = "execute";
    }
    $("harness-info").textContent = data.harnessInfo || data.tuiInfo || "";
    $("sandbox-lazy").checked = data.agentSandboxLazyInit !== false;
    $("workspace").value = data.agentWorkspaceRoot || "";
    $("approval").value = data.agentApprovalMode || "smart";
    $("allow-shell").checked = !!data.agentAllowShell;
    $("adaptive-output").checked = !!data.enableAdaptiveOutputEscalation;
    const intentRoute = $("auto-intent-routing");
    if (intentRoute) intentRoute.checked = data.agentAutoIntentRouting !== false;
    const intentLlm = $("intent-llm-planner");
    if (intentLlm) intentLlm.checked = data.agentIntentUseLlmPlanner === true;
    const intentCache = $("intent-cache");
    if (intentCache) intentCache.checked = data.agentIntentCacheEnabled !== false;
    const promptMinimal = $("prompt-minimal");
    if (promptMinimal) promptMinimal.checked = data.agentPromptMinimalMode !== false;
    const mcpMax = $("mcp-tools-max");
    if (mcpMax) mcpMax.value = data.agentMcpToolsMaxInRequest ?? 8;
    const mcpLines = $("mcp-catalog-lines");
    if (mcpLines) mcpLines.value = data.agentMcpCatalogMaxLines ?? 16;
    const compactK = $("context-compact-k");
    if (compactK) compactK.value = Math.round((data.agentContextCompactTokenThreshold ?? 40960) / 1024);
    const toolOut = $("tool-output-max");
    if (toolOut) toolOut.value = data.agentToolOutputInlineMaxChars ?? 3000;
    const skillMax = $("skill-max-chars");
    if (skillMax) skillMax.value = data.agentSkillMaxChars ?? 3000;
    const snapMax = $("workspace-snapshot-max");
    if (snapMax) snapMax.value = data.agentWorkspaceSnapshotMaxEntries ?? 30;
    const dynGroup = $("enable-dynamic-group");
    if (dynGroup) dynGroup.checked = !!data.enableDynamicGroupChat;
    const lfOn = $("langfuse-enabled");
    if (lfOn) lfOn.checked = !!data.agentLangfuseEnabled;
    const lfHost = $("langfuse-host");
    if (lfHost) lfHost.value = data.agentLangfuseHost || "https://cloud.langfuse.com";
    const lfProj = $("langfuse-project");
    if (lfProj) lfProj.value = data.agentLangfuseProject || "";
    const lfPub = $("langfuse-public-key");
    if (lfPub) lfPub.value = data.agentLangfusePublicKey || "";
    const lfSec = $("langfuse-secret-key");
    if (lfSec) lfSec.value = data.agentLangfuseSecretKey || "";
    const traceEl = $("structured-trace");
    if (traceEl) traceEl.checked = data.agentStructuredTraceEnabled !== false;
    const traceDaysEl = $("trace-retention");
    if (traceDaysEl) traceDaysEl.value = data.agentTraceRetentionDays ?? 30;
    const memEl = $("semantic-memory");
    if (memEl) memEl.checked = data.agentSemanticMemoryEnabled !== false;
    const memAutoEl = $("semantic-memory-auto");
    if (memAutoEl) memAutoEl.checked = !!data.agentSemanticMemoryAutoExtract;
    const topKEl = $("semantic-topk");
    if (topKEl) topKEl.value = data.agentSemanticMemoryTopK ?? 8;
    const embEl = $("embedding-model");
    if (embEl) embEl.value = data.agentEmbeddingModel || "";
    const memTtl = $("memory-session-ttl");
    if (memTtl) memTtl.value = data.agentSemanticMemorySessionTtlDays ?? 7;
    $("config-path").textContent = data.configPath || "";
    renderMcpList(data.mcpServers || []);
    renderFeaturePills(data);
  }

  function collectSavePayload() {
    return {
      maxAgentSteps: parseInt($("max-steps").value, 10) || 30,
      maxSubAgentSteps: parseInt($("max-sub-steps").value, 10) || 10,
      enableSubAgents: $("enable-sub-agents")?.checked !== false,
      enableTeamWorkflow: $("enable-team")?.checked !== false,
      enableParallelExplore: $("enable-parallel-explore")?.checked !== false,
      enableDebateWorkflow: $("enable-debate")?.checked !== false,
      maxConcurrentSubAgents: parseInt($("max-concurrent-sub")?.value, 10) || 3,
      parallelExploreFanOut: parseInt($("parallel-fan-out")?.value, 10) || 3,
      debateMaxRounds: parseInt($("debate-rounds")?.value, 10) || 3,
      defaultAgentStrategy: $("strategy").value || "execute",
      agentInferenceMode: $("inference-mode")?.value || "web",
      agentToolCallingProtocol: $("tool-protocol")?.value || "xml",
      agentApiBaseUrl: $("api-base")?.value?.trim() || "",
      agentApiKey: $("api-key")?.value?.trim() || "",
      agentVisionModel: $("vision-model")?.value?.trim() || "",
      agentVisionApiBaseUrl: $("vision-base")?.value?.trim() || "",
      agentVisionApiKey: $("vision-key")?.value?.trim() || "",
      agentReasoningEffort: $("reasoning-effort")?.value || "max",
      agentThinkingDisplayMode: $("thinking-display")?.value || "normal",
      agentNotifyScript: $("notify-script")?.value?.trim() || "",
      agentSandboxLazyInit: $("sandbox-lazy").checked,
      agentWorkspaceRoot: $("workspace").value.trim(),
      agentApprovalMode: $("approval").value,
      agentAllowShell: $("allow-shell").checked,
      enableAdaptiveOutputEscalation: $("adaptive-output").checked,
      agentAutoIntentRouting: $("auto-intent-routing")?.checked !== false,
      agentIntentUseLlmPlanner: $("intent-llm-planner")?.checked === true,
      agentIntentCacheEnabled: $("intent-cache")?.checked !== false,
      agentPromptMinimalMode: $("prompt-minimal")?.checked !== false,
      agentMcpToolsMaxInRequest: parseInt($("mcp-tools-max")?.value, 10) || 8,
      agentMcpCatalogMaxLines: parseInt($("mcp-catalog-lines")?.value, 10) || 16,
      agentContextCompactTokenThreshold: (parseInt($("context-compact-k")?.value, 10) || 0) * 1024,
      agentToolOutputInlineMaxChars: parseInt($("tool-output-max")?.value, 10) || 3000,
      agentSkillMaxChars: parseInt($("skill-max-chars")?.value, 10) || 3000,
      agentWorkspaceSnapshotMaxEntries: parseInt($("workspace-snapshot-max")?.value, 10) || 30,
      enableDynamicGroupChat: !!$("enable-dynamic-group")?.checked,
      agentLangfuseEnabled: !!$("langfuse-enabled")?.checked,
      agentLangfuseHost: $("langfuse-host")?.value?.trim() || "https://cloud.langfuse.com",
      agentLangfuseProject: $("langfuse-project")?.value?.trim() || "",
      agentLangfusePublicKey: $("langfuse-public-key")?.value?.trim() || "",
      agentLangfuseSecretKey: $("langfuse-secret-key")?.value?.trim() || "",
      agentStructuredTraceEnabled: $("structured-trace")?.checked !== false,
      agentTraceRetentionDays: parseInt($("trace-retention")?.value, 10) || 0,
      agentSemanticMemoryEnabled: $("semantic-memory")?.checked !== false,
      agentSemanticMemoryAutoExtract: !!$("semantic-memory-auto")?.checked,
      agentSemanticMemoryTopK: parseInt($("semantic-topk")?.value, 10) || 8,
      agentEmbeddingModel: $("embedding-model")?.value?.trim() || "",
      agentSemanticMemorySessionTtlDays: parseInt($("memory-session-ttl")?.value, 10) || 0,
    };
  }

  async function reload() {
    const res = await postAsync("settingsLoad", {});
    applyPayload(res);
  }

  async function handleMcpAction(action, id) {
    if (action === "toggle") {
      await postAsync("settingsMcpToggle", { serverId: id });
      await reload();
      return;
    }
    if (action === "edit") {
      await postAsync("settingsMcpEdit", { serverId: id });
      await reload();
      return;
    }
    if (action === "remove") {
      if (!confirm("确定删除此 MCP 服务器？")) return;
      await postAsync("settingsMcpRemove", { serverId: id });
      await reload();
    }
  }

  function bindTabs() {
    const tabs = document.querySelectorAll(".se-tab");
    const panels = document.querySelectorAll(".se-panel");
    tabs.forEach((tab) => {
      tab.addEventListener("click", () => {
        const id = tab.dataset.tab;
        tabs.forEach((t) => {
          t.classList.toggle("is-active", t === tab);
          t.setAttribute("aria-selected", t === tab ? "true" : "false");
        });
        panels.forEach((p) => {
          const on = p.dataset.panel === id;
          p.hidden = !on;
          p.classList.toggle("is-active", on);
        });
      });
    });
  }

  function bindDependentToggles() {
    const intentOn = $("auto-intent-routing");
    const intentLlm = $("intent-llm-planner");
    const intentRow = $("row-intent-llm");
    const intentCacheRow = $("row-intent-cache");
    const subOn = $("enable-sub-agents");
    const parOn = $("enable-parallel-explore");

    function syncIntent() {
      const on = intentOn?.checked !== false;
      if (intentRow) intentRow.classList.toggle("is-disabled", !on);
      if (intentCacheRow) intentCacheRow.classList.toggle("is-disabled", !on);
      if (intentLlm) intentLlm.disabled = !on;
      const intentCache = $("intent-cache");
      if (intentCache) intentCache.disabled = !on;
    }

    function syncSubAgents() {
      const on = subOn?.checked !== false;
      [parOn, $("enable-team"), $("enable-debate")].forEach((el) => {
        if (el) {
          el.disabled = !on;
          el.closest(".se-toggle-row")?.classList.toggle("is-disabled", !on);
        }
      });
    }

    intentOn?.addEventListener("change", syncIntent);
    subOn?.addEventListener("change", syncSubAgents);
    syncIntent();
    syncSubAgents();
  }

  async function init() {
    bindHostMessages();
    bindTabs();
    bindDependentToggles();
    $("btn-save")?.addEventListener("click", async () => {
      try {
        await postAsync("settingsSave", collectSavePayload());
      } catch (e) {
        alert(e.message || "保存失败");
      }
    });
    $("btn-cancel")?.addEventListener("click", () => {
      if (window.parent !== window) {
        window.parent.postMessage(JSON.stringify({ type: "settingsEmbedClose", __dsEmbed: true }), "*");
      } else {
        reload();
      }
    });
    $("btn-connect-all")?.addEventListener("click", async () => {
      try {
        const res = await postAsync("settingsConnectAllMcp", {});
        if (res.summary) alert(res.summary);
        await reload();
      } catch (e) {
        alert(e.message || "连接失败");
      }
    });
    $("btn-add-mcp")?.addEventListener("click", async () => {
      await postAsync("settingsMcpAdd", {});
      await reload();
    });
    $("btn-open-home")?.addEventListener("click", () => post("settingsOpenDeepSeekHome", {}));
    $("btn-docs")?.addEventListener("click", () => post("settingsOpenDocs", {}));
    $("btn-copy-config")?.addEventListener("click", () => post("settingsCopyConfigPath", {}));
    $("btn-open-config")?.addEventListener("click", () => post("settingsOpenConfig", {}));
    $("btn-doctor")?.addEventListener("click", async () => {
      try {
        const res = await postAsync("settingsRunDoctor", {});
        if (res.text) alert(res.text);
      } catch (e) {
        alert(e.message || "doctor 失败");
      }
    });
    try {
      await reload();
    } catch (e) {
      const el = $("harness-info");
      if (el) el.textContent = "加载设置失败：" + (e.message || e);
    }
  }

  init();
})();
