(function () {
  "use strict";

  function post(type, payload) {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(JSON.stringify({ type, ...(payload || {}) }));
    }
  }

  const $ = (id) => document.getElementById(id);

  const pendingRequests = new Map();
  let reqSeq = 0;

  const state = {
    loggedIn: false,
    authResolved: false,
    running: false,
    sessionMetas: [],
    activeSession: null,
    activeSessionId: null,
    currentRun: null,
    files: [],
    strategy: "react",
    storageBytes: 0,
    storageCount: 0,
    selectMode: false,
    selectedIds: new Set(),
  };

  function uid() {
    return "s_" + Date.now().toString(36) + Math.random().toString(36).slice(2, 7);
  }

  function postAsync(type, payload) {
    return new Promise((resolve, reject) => {
      const reqId = "r" + ++reqSeq;
      const timer = setTimeout(() => {
        pendingRequests.delete(reqId);
        reject(new Error("存储请求超时"));
      }, 15000);
      pendingRequests.set(reqId, (msg) => {
        clearTimeout(timer);
        resolve(msg);
      });
      post(type, { ...(payload || {}), reqId });
    });
  }

  function handlePendingReply(msg) {
    if (!msg?.reqId || !pendingRequests.has(msg.reqId)) return false;
    pendingRequests.get(msg.reqId)(msg);
    pendingRequests.delete(msg.reqId);
    return true;
  }

  function formatStorageSize(bytes) {
    if (bytes < 1024) return bytes + " B";
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + " KB";
    if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(2) + " MB";
    return (bytes / (1024 * 1024 * 1024)).toFixed(2) + " GB";
  }

  function updateStorageStats() {
    const el = $("storage-stats");
    if (el) {
      el.textContent =
        state.storageCount + " 条 · " + formatStorageSize(state.storageBytes);
    }
  }

  function sessionGroupLabel(updatedAt) {
    const ts = updatedAt || Date.now();
    const d = new Date(ts);
    const now = new Date();
    const startToday = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
    const startYesterday = startToday - 86400000;
    const t = d.getTime();
    if (t >= startToday) return "今天";
    if (t >= startYesterday) return "昨天";
    if (t >= now.getTime() - 7 * 86400000) return "7 天内";
    if (t >= now.getTime() - 30 * 86400000) return "30 天内";
    return "更早";
  }

  async function migrateLegacyLocalStorage() {
    try {
      const raw = localStorage.getItem("ds-agent-sessions");
      if (!raw) return;
      const sessions = JSON.parse(raw);
      if (!Array.isArray(sessions) || !sessions.length) return;
      const now = Date.now();
      const normalized = sessions.map((s) => ({
        id: s.id || uid(),
        title: s.title || "新对话",
        createdAt: s.createdAt || now,
        updatedAt: s.updatedAt || now,
        messages: s.messages || [],
      }));
      await postAsync("agentStorageMigrate", { sessions: normalized });
      localStorage.removeItem("ds-agent-sessions");
    } catch (_) {}
  }

  async function refreshSessionList() {
    const res = await postAsync("agentStorageList", {});
    state.sessionMetas = Array.isArray(res.sessions) ? res.sessions : [];
    state.storageBytes = res.totalBytes || 0;
    state.storageCount = res.count || state.sessionMetas.length;
    updateStorageStats();
    renderSessions();
  }

  function getActiveSessionForSave() {
    if (!state.activeSessionId || !state.activeSession) return null;
    return {
      id: state.activeSessionId,
      title: state.activeSession.title || "新对话",
      createdAt: state.activeSession.createdAt || Date.now(),
      updatedAt: Date.now(),
      messages: state.activeSession.messages || [],
    };
  }

  async function persistActiveSession() {
    const payload = getActiveSessionForSave();
    if (!payload) return;
    const res = await postAsync("agentStorageSave", { session: payload });
    state.storageBytes = res.totalBytes ?? state.storageBytes;
    state.storageCount = res.count ?? state.storageCount;
    const meta = state.sessionMetas.find((m) => m.id === payload.id);
    if (meta) {
      meta.title = payload.title;
      meta.updatedAt = payload.updatedAt;
    } else {
      state.sessionMetas.unshift({
        id: payload.id,
        title: payload.title,
        updatedAt: payload.updatedAt,
        createdAt: payload.createdAt,
      });
    }
    updateStorageStats();
    renderSessions();
  }

  function renderSessions() {
    const list = $("session-list");
    if (!list) return;
    list.innerHTML = "";

    const groups = new Map();
    state.sessionMetas.forEach((s) => {
      const label = sessionGroupLabel(s.updatedAt || s.createdAt);
      if (!groups.has(label)) groups.set(label, []);
      groups.get(label).push(s);
    });

    const order = ["今天", "昨天", "7 天内", "30 天内", "更早"];
    order.forEach((label) => {
      const items = groups.get(label);
      if (!items?.length) return;

      const gl = document.createElement("div");
      gl.className = "ds-session-group-label";
      gl.textContent = label;
      list.appendChild(gl);

      items.forEach((s) => {
        const row = document.createElement("div");
        row.className = "ds-session-row";

        if (state.selectMode) {
          const cb = document.createElement("input");
          cb.type = "checkbox";
          cb.className = "ds-session-check";
          cb.checked = state.selectedIds.has(s.id);
          cb.onchange = () => {
            if (cb.checked) state.selectedIds.add(s.id);
            else state.selectedIds.delete(s.id);
          };
          row.appendChild(cb);
        }

        const btn = document.createElement("button");
        btn.type = "button";
        btn.className =
          "ds-session-item" + (s.id === state.activeSessionId ? " ds-active" : "");
        btn.textContent = s.title || "新对话";
        btn.title = s.title || "";
        btn.onclick = () => {
          if (state.selectMode) {
            const cb = row.querySelector(".ds-session-check");
            if (cb) {
              cb.checked = !cb.checked;
              if (cb.checked) state.selectedIds.add(s.id);
              else state.selectedIds.delete(s.id);
            }
          } else {
            loadSession(s.id);
          }
        };
        row.appendChild(btn);
        list.appendChild(row);
      });
    });
  }

  function setSelectMode(on) {
    state.selectMode = !!on;
    state.selectedIds.clear();
    $("storage-bar")?.classList.toggle("ds-hidden", state.selectMode);
    $("manage-bar")?.classList.toggle("ds-hidden", !state.selectMode);
    $("btn-manage-sessions")?.classList.toggle("ds-hidden", state.selectMode);
    renderSessions();
  }

  function scrollToBottom() {
    const el = $("chat-scroll");
    if (el) el.scrollTop = el.scrollHeight;
  }

  function hideEmptyState() {
    $("empty-state")?.classList.add("ds-hidden");
  }

  function showEmptyState() {
    $("empty-state")?.classList.remove("ds-hidden");
  }

  function updateComposerState() {
    const input = $("chat-input");
    const send = $("btn-send");
    const hasText = input && input.value.trim().length > 0;
    const hasFiles = state.files.length > 0;
    const canSend = state.loggedIn && !state.running && (hasText || hasFiles);
    if (send) send.disabled = !canSend;
  }

  function renderFileChips() {
    const box = $("file-chips");
    if (!box) return;
    if (!state.files.length) {
      box.hidden = true;
      box.innerHTML = "";
      return;
    }
    box.hidden = false;
    box.innerHTML = "";
    state.files.forEach((f, idx) => {
      const chip = document.createElement("span");
      chip.className = "ds-file-chip";
      const name = document.createElement("span");
      name.textContent = f.name;
      const rm = document.createElement("button");
      rm.type = "button";
      rm.setAttribute("aria-label", "移除附件");
      rm.textContent = "×";
      rm.onclick = () => {
        state.files.splice(idx, 1);
        renderFileChips();
        updateComposerState();
      };
      chip.append(name, rm);
      box.appendChild(chip);
    });
  }

  async function handleFiles(fileList) {
    if (!fileList?.length) return;
    if (!window.dsDesktopBridge?.uploadUserFile) {
      alert("文件上传不可用，请重启应用后重试。");
      return;
    }
    for (const file of fileList) {
      try {
        const id = await window.dsDesktopBridge.uploadUserFile(file);
        state.files.push({ id, name: file.name });
      } catch (e) {
        alert("上传失败: " + (e.message || e));
      }
    }
    renderFileChips();
    updateComposerState();
  }

  function setLoggedIn(online) {
    state.loggedIn = !!online;
    state.authResolved = true;
    const dot = $("login-dot");
    const label = $("login-label");
    const banner = $("login-banner");
    if (dot) dot.classList.toggle("ds-online", state.loggedIn);
    if (label) label.textContent = state.loggedIn ? "已登录" : "未登录";
    if (banner) {
      const hide = state.loggedIn;
      banner.hidden = hide;
      banner.classList.toggle("ds-hidden", hide);
      banner.style.display = hide ? "none" : "flex";
      banner.setAttribute("aria-hidden", hide ? "true" : "false");
    }
    updateComposerState();
  }

  window.dsAgentApplyAuth = function (online) {
    setLoggedIn(!!online);
  };

  function flushPendingNativeMessages() {
    const q = window.__dsPendingNativeMessages;
    if (!Array.isArray(q) || !q.length) return;
    window.__dsPendingNativeMessages = [];
    q.forEach((m) => {
      try {
        window.dsDesktopOnMessage && window.dsDesktopOnMessage(m);
      } catch (_) {}
    });
  }

  const THINK_ICON_SVG =
    '<svg viewBox="0 0 24 24" fill="none"><circle cx="12" cy="12" r="10" fill="#EEF2FF"/><path d="M8 12h8M12 8v8" stroke="#4D6BFE" stroke-width="1.6" stroke-linecap="round"/></svg>';

  function truncate(s, max) {
    if (!s) return "";
    return s.length <= max ? s : s.slice(0, max) + "…";
  }

  function parseAgentLog(text) {
    const t = (text || "").trim();
    if (!t) return null;
    let m;
    m = t.match(/^--- ReAct 第 (\d+) 步 ---$/);
    if (m) return { kind: "section", label: "ReAct 第 " + m[1] + " 步" };
    m = t.match(/^══\s*子 Agent · 步骤 (.+?) ══$/);
    if (m) return { kind: "section", label: "子 Agent · " + m[1] };
    m = t.match(/^思考:\s*(.+)$/s);
    if (m) return { kind: "prose", text: m[1].trim() };
    m = t.match(/^Thought:\s*(.+)$/s);
    if (m) return { kind: "prose", text: m[1].trim() };
    m = t.match(/^Action:\s*调用\s*(\d+)\s*个工具/);
    if (m) return { kind: "step", verb: "Action", target: m[1] + " 个 MCP 工具" };
    m = t.match(/^Calling\s+(.+)$/i);
    if (m) return { kind: "step", verb: "Calling", target: m[1].trim() };
    m = t.match(/^→\s*(.+)$/);
    if (m) return { kind: "step", verb: "Calling", target: m[1].trim() };
    m = t.match(/^Observation:\s*(.+)$/s);
    if (m) return { kind: "step", verb: "Result", target: m[1].trim(), muted: true };
    if (/^Final Answer:/i.test(t)) return { kind: "skip" };
    m = t.match(/^策略:\s*(.+)$/);
    if (m) return { kind: "step", verb: "Strategy", target: m[1].trim() };
    m = t.match(/^\[计划模式\]\s*(.+)$/);
    if (m) return { kind: "step", verb: "Plan", target: m[1].trim() };
    m = t.match(/^  ·\s*(.+)$/);
    if (m) return { kind: "step", verb: "Step", target: m[1].trim(), muted: true };
    m = t.match(/^MCP 工具注册表:\s*(.+)$/);
    if (m) return { kind: "step", verb: "MCP", target: m[1].trim() };
    m = t.match(/^工具范围:\s*(.+)$/);
    if (m) return { kind: "step", verb: "Tools", target: m[1].trim(), muted: true };
    if (/^已连接/.test(t)) return { kind: "step", verb: "MCP", target: t };
    if (/^连接失败:/.test(t)) return { kind: "step", verb: "Error", target: t, error: true };
    if (/^错误:/.test(t)) return { kind: "step", verb: "Error", target: t, error: true };
    return { kind: "step", verb: "Log", target: t, muted: true };
  }

  function updateThinkTitle(run) {
    if (!run?.titleEl) return;
    const sec = Math.max(1, Math.floor((Date.now() - run.thinkStartTime) / 1000));
    run.titleEl.textContent = run.thinkDone
      ? "已思考（用时 " + sec + " 秒）"
      : "思考中…（" + sec + " 秒）";
  }

  function fadeThinkSteps(stepsEl) {
    if (!stepsEl) return;
    const items = [...stepsEl.querySelectorAll(".ds-step")];
    items.forEach((el, i) => {
      el.classList.toggle("ds-step-faded", i < items.length - 10);
    });
  }

  function pushThinkRecord(run, entry) {
    if (!run.thinkRecords) run.thinkRecords = [];
    run.thinkRecords.push(entry);
    if (run.thinkRecords.length > 200) run.thinkRecords.shift();
  }

  function renderThinkSection(stepsEl, label) {
    const h = document.createElement("div");
    h.className = "ds-step-section";
    h.textContent = label;
    stepsEl.appendChild(h);
    return { kind: "section", label };
  }

  function renderThinkStep(stepsEl, step) {
    const row = document.createElement("div");
    row.className = "ds-step" + (step.error ? " ds-step-error" : "");
    const verb = document.createElement("span");
    verb.className = "ds-step-verb";
    verb.textContent = step.verb;
    const target = document.createElement("span");
    target.className = "ds-step-target";
    target.textContent = truncate(step.target, 220);
    row.append(verb, target);
    stepsEl.appendChild(row);
    fadeThinkSteps(stepsEl);
    return { kind: "step", verb: step.verb, target: step.target, error: !!step.error };
  }

  function applyThinkLog(run, parsed) {
    if (!run || !parsed || parsed.kind === "skip") return;
    if (parsed.kind === "prose") {
      run.proseEl.textContent = parsed.text;
      pushThinkRecord(run, { kind: "prose", text: parsed.text });
    } else if (parsed.kind === "section") {
      renderThinkSection(run.stepsEl, parsed.label);
      pushThinkRecord(run, { kind: "section", label: parsed.label });
    } else if (parsed.kind === "step") {
      pushThinkRecord(run, renderThinkStep(run.stepsEl, parsed));
    }
    updateThinkTitle(run);
    scrollToBottom();
  }

  function appendLogLine(text) {
    const run = state.currentRun;
    if (!run?.stepsEl) return;
    applyThinkLog(run, parseAgentLog(text));
  }

  function createThinkBlock() {
    const think = document.createElement("div");
    think.className = "ds-think";
    const header = document.createElement("button");
    header.type = "button";
    header.className = "ds-think-header";
    header.setAttribute("aria-expanded", "true");
    const icon = document.createElement("span");
    icon.className = "ds-think-icon";
    icon.innerHTML = THINK_ICON_SVG;
    const title = document.createElement("span");
    title.className = "ds-think-title";
    title.textContent = "思考中…";
    const chevron = document.createElement("span");
    chevron.className = "ds-think-chevron";
    chevron.innerHTML =
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 9l6 6 6-6"/></svg>';
    header.append(icon, title, chevron);
    const body = document.createElement("div");
    body.className = "ds-think-body";
    const prose = document.createElement("div");
    prose.className = "ds-think-prose";
    const steps = document.createElement("div");
    steps.className = "ds-think-steps";
    body.append(prose, steps);
    think.append(header, body);
    header.addEventListener("click", () => {
      const collapsed = think.classList.toggle("ds-collapsed");
      header.setAttribute("aria-expanded", collapsed ? "false" : "true");
    });
    const tick = setInterval(() => {
      if (!think.isConnected || state.currentRun?.thinkEl !== think) {
        clearInterval(tick);
        return;
      }
      updateThinkTitle(state.currentRun);
    }, 1000);
    return {
      thinkEl: think,
      titleEl: title,
      proseEl: prose,
      stepsEl: steps,
      thinkStartTime: Date.now(),
      thinkDone: false,
      thinkRecords: [],
    };
  }

  function finalizeThinkBlock(run) {
    if (!run) return;
    run.thinkDone = true;
    updateThinkTitle(run);
    run.thinkEl?.classList.add("ds-think-done");
  }

  function restoreThinkBlock(container, data) {
    if (!data?.records?.length) return null;
    const think = createThinkBlock();
    think.thinkStartTime = Date.now() - (data.durationSec || 1) * 1000;
    think.thinkDone = true;
    container.appendChild(think.thinkEl);
    data.records.forEach((rec) => {
      if (rec.kind === "prose") think.proseEl.textContent = rec.text;
      else if (rec.kind === "section") renderThinkSection(think.stepsEl, rec.label);
      else if (rec.kind === "step") {
        const row = document.createElement("div");
        row.className = "ds-step" + (rec.error ? " ds-step-error" : "");
        const verb = document.createElement("span");
        verb.className = "ds-step-verb";
        verb.textContent = rec.verb;
        const target = document.createElement("span");
        target.className = "ds-step-target";
        target.textContent = truncate(rec.target, 220);
        row.append(verb, target);
        think.stepsEl.appendChild(row);
      }
    });
    updateThinkTitle(think);
    think.thinkRecords = data.records.slice();
    return think;
  }

  function appendMessage(role, text, extra) {
    hideEmptyState();
    const wrap = $("messages");
    if (!wrap) return null;

    const row = document.createElement("div");
    row.className = "ds-msg-row ds-" + role;

    const bubble = document.createElement("div");
    bubble.className = "ds-msg-bubble";

    if (role === "user") {
      bubble.textContent = text;
      row.appendChild(bubble);
      wrap.appendChild(row);
      scrollToBottom();
      return { row, bubble };
    }

    const think = createThinkBlock();
    const answer = document.createElement("div");
    answer.className = "ds-msg-answer";

    bubble.append(think.thinkEl, answer);
    row.appendChild(bubble);
    wrap.appendChild(row);
    scrollToBottom();

    return { row, bubble, answerEl: answer, ...think };
  }

  function prepareEmptyChat() {
    state.files = [];
    renderFileChips();
    const now = Date.now();
    state.activeSessionId = uid();
    state.activeSession = {
      id: state.activeSessionId,
      title: "新对话",
      createdAt: now,
      updatedAt: now,
      messages: [],
    };
    $("messages").innerHTML = "";
    showEmptyState();
    $("chat-input").value = "";
    updateComposerState();
    renderSessions();
  }

  async function startNewChat() {
    if (state.running) return;
    await persistActiveSession().catch(() => {});
    prepareEmptyChat();
  }

  async function loadSession(id) {
    if (state.running) return;
    await persistActiveSession().catch(() => {});
    const res = await postAsync("agentStorageLoad", { id });
    const s = res.session;
    if (!s) return;
    state.activeSessionId = id;
    state.activeSession = {
      id: s.id,
      title: s.title || "新对话",
      createdAt: s.createdAt || Date.now(),
      updatedAt: s.updatedAt || Date.now(),
      messages: s.messages || [],
    };
    renderSessions();
    $("messages").innerHTML = "";
    if (!state.activeSession.messages?.length) {
      showEmptyState();
    } else {
      hideEmptyState();
      state.activeSession.messages.forEach((m) => {
        if (m.role === "user") appendMessage("user", m.text);
        else if (m.role === "assistant") {
          const row = document.createElement("div");
          row.className = "ds-msg-row ds-assistant";
          const bubble = document.createElement("div");
          bubble.className = "ds-msg-bubble";
          if (m.think?.records?.length) {
            restoreThinkBlock(bubble, {
              records: m.think.records,
              durationSec: m.think.durationSec || 1,
            });
          }
          if (m.answer) {
            const answer = document.createElement("div");
            answer.className = "ds-msg-answer";
            answer.textContent = m.answer;
            bubble.appendChild(answer);
          }
          row.appendChild(bubble);
          $("messages")?.appendChild(row);
        }
      });
    }
    scrollToBottom();
  }

  function ensureSession() {
    if (!state.activeSessionId || !state.activeSession) {
      const now = Date.now();
      state.activeSessionId = uid();
      state.activeSession = {
        id: state.activeSessionId,
        title: "新对话",
        createdAt: now,
        updatedAt: now,
        messages: [],
      };
    }
    return state.activeSession;
  }

  function qwenCodeHelpText() {
    return "DeepSeek Agent + Qwen Code Core（C# 移植）\n/help /clear /react /plan /chat\n/skills [/name]  /agents <name>\n!git status  直接执行 Shell\n@文件 附加工作区\n推理：Chat2API · 工具：Core + MCP + Skills/Subagents";
  }

  async function handleSlashCommand(text) {
    const t = text.trim();
    if (!t.startsWith("/")) return false;
    const cmd = t.split(/\s+/)[0].toLowerCase();
    if (cmd === "/help") {
      appendMessage("assistant", qwenCodeHelpText(), { status: "Qwen Code 帮助" });
      return true;
    }
    if (cmd === "/clear") {
      await startNewChat();
      return true;
    }
    if (cmd === "/react") {
      state.strategy = "react";
      appendMessage("assistant", "已切换为 ReAct 单 Agent 模式。", { status: "模式" });
      return true;
    }
    if (cmd === "/plan") {
      state.strategy = "plan";
      appendMessage("assistant", "已切换为计划 + 子 Agent 模式。", { status: "模式" });
      return true;
    }
    if (cmd === "/chat") {
      post("setWorkMode", { mode: "chat" });
      return true;
    }
    if (cmd === "/skills" || cmd === "/skill" || cmd === "/agents" || cmd === "/agent") {
      return false;
    }
    return false;
  }

  async function dispatchRun() {
    const input = $("chat-input");
    const text = (input?.value || "").trim();
    if (!text || state.running || !state.loggedIn) return;

    if (await handleSlashCommand(text)) {
      input.value = "";
      resizeInput();
      return;
    }

    const session = ensureSession();
    if (session && session.title === "新对话") {
      session.title = text.slice(0, 32) + (text.length > 32 ? "…" : "");
      renderSessions();
    }

    appendMessage("user", text);
    session.messages = session.messages || [];
    session.messages.push({ role: "user", text });
    await persistActiveSession().catch(() => {});

    state.currentRun = appendMessage("assistant", "", { status: "Agent 正在执行（Chat2API + MCP）…" });
    state.running = true;
    updateComposerState();
    input.value = "";
    resizeInput();

    const refFileIds = state.files.map((f) => f.id);
    state.files = [];
    renderFileChips();

    post("setWorkMode", { mode: "agent" });
    post("agentRun", {
      text: text || (refFileIds.length ? "请阅读附件并完成任务。" : text),
      mode: "专家",
      strategy: state.strategy,
      deepThink: true,
      smartSearch: true,
      mcpOn: true,
      refFileIds,
    });
  }

  function resizeInput() {
    const input = $("chat-input");
    if (!input) return;
    input.style.height = "auto";
    input.style.height = Math.min(200, Math.max(52, input.scrollHeight)) + "px";
  }

  function finishRun(summary, answer) {
    state.running = false;
    updateComposerState();

    const text = (answer || summary || "任务已结束").trim();
    const failed = /^(失败|错误|已停止)/.test(text);

    if (state.currentRun) {
      if (failed) {
        applyThinkLog(state.currentRun, {
          kind: "step",
          verb: "Error",
          target: text.split("\n")[0],
          error: true,
        });
      }
      finalizeThinkBlock(state.currentRun);
      const durationSec = Math.max(
        1,
        Math.floor((Date.now() - state.currentRun.thinkStartTime) / 1000)
      );
      if (text && !/^任务已结束$/.test(text)) {
        state.currentRun.answerEl.textContent = text;
      }

      const session = state.activeSession;
      if (session) {
        session.messages = session.messages || [];
        session.messages.push({
          role: "assistant",
          answer: failed ? "" : text,
          think: {
            records: state.currentRun.thinkRecords || [],
            durationSec,
          },
        });
        persistActiveSession().catch(() => {});
      }
    }

    state.currentRun = null;
    scrollToBottom();
  }

  window.dsDesktopOnMessage = function (msg) {
    if (!msg || !msg.type) return;
    if (handlePendingReply(msg)) return;

    if (msg.type === "apiInfo" || msg.type === "loginState") {
      setLoggedIn(!!msg.loggedIn);
      if (msg.type === "apiInfo" && msg.agentStrategy) {
        state.strategy = msg.agentStrategy === "plan" ? "plan" : "react";
      }
    }

    if (msg.type === "agentLog" && msg.text) {
      appendLogLine(msg.text);
    }

    if (msg.type === "agentAnswer" && msg.text) {
      if (state.currentRun?.answerEl) {
        state.currentRun.answerEl.textContent = msg.text;
      }
    }

    if (msg.type === "agentStarted") {
      updateThinkTitle(state.currentRun);
    }

    if (msg.type === "agentDone") {
      finishRun(msg.summary, msg.answer);
    }
  };

  function bindUi() {
    $("btn-send")?.addEventListener("click", () => dispatchRun());
    $("btn-new-chat")?.addEventListener("click", () => startNewChat());
    $("btn-mcp")?.addEventListener("click", () => post("openSettings", {}));
    $("btn-storage-settings")?.addEventListener("click", () => post("openSettings", {}));
    $("btn-mode-chat")?.addEventListener("click", () => post("setWorkMode", { mode: "chat" }));
    $("btn-goto-chat")?.addEventListener("click", () => post("setWorkMode", { mode: "chat" }));
    $("mode-float")?.addEventListener("click", () => post("setWorkMode", { mode: "chat" }));
    $("btn-login-goto")?.addEventListener("click", () => post("setWorkMode", { mode: "chat" }));

    const fileInput = $("file-input");
    const attachBtn = $("btn-attach");
    attachBtn?.addEventListener("click", () => fileInput?.click());
    fileInput?.addEventListener("change", () => {
      if (fileInput.files?.length) handleFiles([...fileInput.files]);
      fileInput.value = "";
    });

    const input = $("chat-input");
    input?.addEventListener("input", () => {
      resizeInput();
      updateComposerState();
    });
    input?.addEventListener("keydown", (e) => {
      if (e.key === "Enter" && !e.shiftKey && !e.isComposing) {
        e.preventDefault();
        dispatchRun();
      }
    });

    $("btn-manage-sessions")?.addEventListener("click", () => setSelectMode(true));
    $("btn-manage-done")?.addEventListener("click", () => setSelectMode(false));
    $("btn-select-all")?.addEventListener("click", () => {
      state.sessionMetas.forEach((s) => state.selectedIds.add(s.id));
      renderSessions();
    });
    $("btn-delete-selected")?.addEventListener("click", async () => {
      const ids = [...state.selectedIds];
      if (!ids.length) {
        alert("请先勾选要删除的对话。");
        return;
      }
      if (!confirm("确定删除所选 " + ids.length + " 条对话？此操作不可恢复。")) return;
      const res = await postAsync("agentStorageDelete", { ids });
      state.sessionMetas = res.sessions || [];
      state.storageBytes = res.totalBytes || 0;
      state.storageCount = res.count || 0;
      if (ids.includes(state.activeSessionId)) {
        state.activeSessionId = null;
        state.activeSession = null;
        if (state.sessionMetas.length) {
          await loadSession(state.sessionMetas[0].id);
        } else {
          await startNewChat();
        }
      }
      setSelectMode(false);
      updateStorageStats();
      renderSessions();
    });
  }

  async function bootstrapStorage() {
    try {
      await migrateLegacyLocalStorage();
      await refreshSessionList();
      if (state.sessionMetas.length) {
        const first = state.sessionMetas[0];
        if (first?.id && first.id !== state.activeSessionId) {
          await loadSession(first.id);
        } else {
          renderSessions();
        }
      }
    } catch (e) {
      console.warn("Agent storage init:", e);
    }
  }

  async function init() {
    bindUi();
    resizeInput();
    prepareEmptyChat();

    const banner = $("login-banner");
    if (banner) {
      banner.hidden = true;
      banner.style.display = "none";
    }
    if (window.__dsLoggedIn === true) setLoggedIn(true);
    else if (window.__dsLoggedIn === false) setLoggedIn(false);

    post("nativeReady", {});
    post("refreshLoginState", {});
    flushPendingNativeMessages();

    void bootstrapStorage();

    setInterval(() => post("refreshLoginState", {}), 8000);
    setTimeout(() => {
      if (!state.authResolved) post("refreshLoginState", {});
    }, 1500);
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", () => init());
  } else {
    init();
  }

  flushPendingNativeMessages();
})();
