# -*- coding: utf-8 -*-
import pathlib

p = pathlib.Path(__file__).resolve().parent.parent / "Assets" / "inject" / "overlay.js"
text = p.read_text(encoding="utf-8")

insert = r'''
  function setWorkMode(mode, notify) {
    workMode = mode === "agent" ? "agent" : "chat";
    try { localStorage.setItem("ds-work-mode", workMode); } catch (_) {}
    post("setWorkMode", { mode: workMode });
    syncWorkModeUi();
    if (notify) {
      showToast(
        workMode === "agent" ? "Agent 智能体" : "网页对话",
        workMode === "agent"
          ? ["发送走 Chat2API + MCP", "下拉可打开 Agent 工作台"]
          : ["已恢复网页对话"]
      );
    }
  }

  function syncWorkModeUi() {
    const on = workMode === "agent";
    ["ds-work-mode", "ds-work-mode-dock"].forEach((id) => {
      const btn = document.getElementById(id);
      if (!btn) return;
      const label = btn.querySelector(".ds-native-label");
      if (label) label.textContent = on ? "Agent" : "对话";
      applyBtnStyle(btn, on);
      btn.classList.toggle("ds-on", on);
    });
    document.querySelectorAll(".ds-mode-item[data-mode]").forEach((el) => {
      el.classList.toggle("ds-mode-active", el.getAttribute("data-mode") === workMode);
    });
  }

  function closeModeMenus() {
    modeMenuOpen = false;
    document.querySelectorAll(".ds-mode-menu").forEach((m) => { m.style.display = "none"; });
  }

  function createModeSelector(btnId, menuId) {
    const wrap = document.createElement("div");
    wrap.className = "ds-mode-wrap";
    wrap.style.cssText = "position:relative;display:inline-flex";
    const btn = createBtn(btnId, workMode === "agent" ? "Agent" : "对话", "agent", false);
    applyBtnStyle(btn, workMode === "agent");
    if (workMode === "agent") btn.classList.add("ds-on");
    const chev = document.createElement("span");
    chev.textContent = "▾";
    chev.style.cssText = "font-size:10px;margin-left:2px;opacity:.7";
    btn.querySelector(".ds-native-label")?.after(chev);
    const menu = document.createElement("div");
    menu.id = menuId;
    menu.className = "ds-mode-menu";
    menu.style.cssText = "display:none;position:absolute;left:0;top:calc(100% + 6px);min-width:160px;background:#fff;border:1px solid #e5e7eb;border-radius:12px;box-shadow:0 8px 24px rgba(0,0,0,.12);padding:6px;z-index:2147483647";
    function addItem(label, handler, modeAttr) {
      const item = document.createElement("button");
      item.type = "button";
      item.className = "ds-mode-item";
      if (modeAttr) item.setAttribute("data-mode", modeAttr);
      item.textContent = label;
      item.style.cssText = "display:block;width:100%;text-align:left;padding:8px 12px;border:none;background:transparent;border-radius:8px;font-size:13px;color:#374151;cursor:pointer";
      item.onclick = (e) => { e.stopPropagation(); handler(); closeModeMenus(); };
      menu.appendChild(item);
    }
    addItem("对话", () => setWorkMode("chat", true), "chat");
    addItem("Agent 智能体", () => setWorkMode("agent", true), "agent");
    addItem("打开 Agent 工作台…", () => post("openAgentWorkspace", {}), null);
    btn.addEventListener("click", (e) => {
      e.stopPropagation();
      modeMenuOpen = !modeMenuOpen;
      document.querySelectorAll(".ds-mode-menu").forEach((m) => { m.style.display = "none"; });
      menu.style.display = modeMenuOpen ? "block" : "none";
    });
    wrap.append(btn, menu);
    return wrap;
  }

  function ensureAgentLogPanel() {
    let panel = document.getElementById("ds-agent-log");
    if (panel) return panel;
    panel = document.createElement("div");
    panel.id = "ds-agent-log";
    panel.style.cssText = "position:fixed;right:20px;bottom:88px;width:340px;max-height:280px;background:#fff;border:1px solid #e5e7eb;border-radius:12px;box-shadow:0 8px 24px rgba(0,0,0,.12);z-index:2147483646;display:none;flex-direction:column;font-size:12px";
    const head = document.createElement("div");
    head.style.cssText = "padding:10px 12px;font-weight:600;color:#111827;border-bottom:1px solid #f3f4f6";
    head.textContent = "Agent 执行中";
    const body = document.createElement("div");
    body.id = "ds-agent-log-body";
    body.style.cssText = "padding:10px 12px;overflow-y:auto;max-height:220px;color:#374151;line-height:1.5;font-family:Consolas,monospace";
    panel.append(head, body);
    document.body.appendChild(panel);
    return panel;
  }

  function appendAgentLog(text) {
    ensureAgentLogPanel().style.display = "flex";
    const body = document.getElementById("ds-agent-log-body");
    if (!body) return;
    const line = document.createElement("div");
    line.style.marginBottom = "4px";
    line.textContent = text;
    body.appendChild(line);
    while (body.children.length > 80) body.removeChild(body.firstChild);
    body.scrollTop = body.scrollHeight;
  }

  function hideAgentLogPanel() {
    const panel = document.getElementById("ds-agent-log");
    if (panel) panel.style.display = "none";
    const body = document.getElementById("ds-agent-log-body");
    if (body) body.textContent = "";
  }

'''.replace('createElement("motion")', 'createElement("motion")').replace('createElement("motion")', 'createElement("div")', 2)

if "function setWorkMode" not in text:
    text = text.replace("  function buildToolbar() {", insert + "  function buildToolbar() {", 1)

old_build = """    const apiBtn = createBtn("ds-local-api", "本地 API", "api", true);
    const mcpBtn = createBtn("ds-mcp-homework", "MCP 作业", "mcp", false);
    const settingsBtn = createBtn("ds-settings", "MCP 设置", "settings", false);

    apiBtn.addEventListener("click", (e) => {
      e.stopPropagation();
      updateApiDot();
      post("showProviderCard", {});
    });

    mcpBtn.addEventListener("click", (e) => {
      e.stopPropagation();
      mcpHomeworkOn = !mcpHomeworkOn;
      applyBtnStyle(mcpBtn, mcpHomeworkOn);
      mcpBtn.classList.toggle("ds-on", mcpHomeworkOn);
      const dockMcp = document.getElementById("ds-mcp-homework-dock");
      if (dockMcp) {
        applyBtnStyle(dockMcp, mcpHomeworkOn);
        dockMcp.classList.toggle("ds-on", mcpHomeworkOn);
      }
      showToast(mcpHomeworkOn ? "MCP 作业已开启" : "MCP 作业已关闭", [
        mcpHomeworkOn ? "输入任务后点发送即可" : "",
      ]);
    });

    settingsBtn.addEventListener("click", (e) => {
      e.stopPropagation();
      post("openSettings", {});
    });

    wrap.append(apiBtn, mcpBtn, settingsBtn);
    return wrap;"""

new_build = """    const apiBtn = createBtn("ds-local-api", "本地 API", "api", true);
    const modeWrap = createModeSelector("ds-work-mode", "ds-mode-menu");
    const settingsBtn = createBtn("ds-settings", "MCP 设置", "settings", false);

    apiBtn.addEventListener("click", (e) => {
      e.stopPropagation();
      updateApiDot();
      post("showProviderCard", {});
    });

    settingsBtn.addEventListener("click", (e) => {
      e.stopPropagation();
      post("openSettings", {});
    });

    wrap.append(apiBtn, modeWrap, settingsBtn);
    syncWorkModeUi();
    return wrap;"""

text = text.replace(old_build, new_build)
text = text.replace(
    '    const toast = document.createElement("div");\n    toast = document.createElement("motion");',
    '    const toast = document.createElement("div");',
)

old_dock = """    const mcpBtn = createBtn("ds-mcp-homework-dock", "MCP 作业", "mcp", false);
    const settingsBtn = createBtn("ds-settings-dock", "MCP 设置", "settings", false);

    apiBtn.onclick = () => { updateApiDot(); post("showProviderCard", {}); };
    mcpBtn.onclick = () => {
      mcpHomeworkOn = !mcpHomeworkOn;
      mcpBtn.classList.toggle("ds-on", mcpHomeworkOn);
      document.getElementById("ds-mcp-homework")?.classList.toggle("ds-on", mcpHomeworkOn);
      showToast(mcpHomeworkOn ? "MCP 作业已开启" : "MCP 作业已关闭", []);
    };
    settingsBtn.onclick = () => post("openSettings", {});

    dock.append(apiBtn, mcpBtn, settingsBtn);"""

new_dock = """    const modeWrap = createModeSelector("ds-work-mode-dock", "ds-mode-menu-dock");
    const settingsBtn = createBtn("ds-settings-dock", "MCP 设置", "settings", false);

    apiBtn.onclick = () => { updateApiDot(); post("showProviderCard", {}); };
    settingsBtn.onclick = () => post("openSettings", {});

    dock.append(apiBtn, modeWrap, settingsBtn);
    syncWorkModeUi();"""

text = text.replace(old_dock, new_dock)
text = text.replace("        if (!mcpHomeworkOn) return;", '        if (workMode !== "agent") return;')
text = text.replace(
    '        showToast("MCP 作业执行中", ["任务: " + text.slice(0, 80)]);',
    '        hideAgentLogPanel();\n        appendAgentLog("任务: " + text.slice(0, 120));\n        showToast("Agent 执行中", ["任务: " + text.slice(0, 80)]);',
)
text = text.replace(
    '    desc.textContent = "网页 User Token 自动转为本地 OpenAI API，MCP 可操控本机作业。";',
    '    desc.textContent = "网页 Token → 本地 Chat2API。Agent 模式通过 MCP 多步完成本机/Unity 等任务。";',
)

old_msg = """  window.dsDesktopOnMessage = function (msg) {
    if (msg.type === "log") showToast("Agent", [msg.text]);
    if (msg.type === "apiInfo") { apiUrl = msg.url || apiUrl; updateApiDot(); }
    if (msg.type === "showProviderCard") {
      showProviderCard({ url: apiUrl, loggedIn: msg.loggedIn !== false && updateApiDot() });
    }
    if (msg.type === "agentDone") showToast("完成", [msg.summary || "任务已结束"]);
    if (msg.type === "reinject") scheduleBurstInject(false);
  };"""

new_msg = """  window.dsDesktopOnMessage = function (msg) {
    if (msg.type === "log" || msg.type === "agentLog") {
      if (msg.text) appendAgentLog(msg.text);
    }
    if (msg.type === "agentStarted") {
      hideAgentLogPanel();
      appendAgentLog("开始: " + (msg.task || ""));
    }
    if (msg.type === "apiInfo") { apiUrl = msg.url || apiUrl; updateApiDot(); }
    if (msg.type === "showProviderCard") {
      showProviderCard({ url: apiUrl, loggedIn: msg.loggedIn !== false && updateApiDot() });
    }
    if (msg.type === "agentDone") {
      appendAgentLog(msg.summary || "任务已结束");
      showToast("Agent 完成", [msg.summary || "任务已结束"]);
    }
    if (msg.type === "reinject") scheduleBurstInject(false);
  };"""

text = text.replace(old_msg, new_msg)

if 'document.addEventListener("click", closeModeMenus' not in text:
    text = text.replace(
        "    hookSpaNavigation();",
        '    document.addEventListener("click", closeModeMenus);\n    hookSpaNavigation();',
    )

text = text.replace('const wrap = document.createElement("motion");', 'const wrap = document.createElement("div");', 1)
text = text.replace('const body = document.createElement("motion");', 'const body = document.createElement("motion");', 1)
text = text.replace('body.id = "ds-agent-log-body";', 'body.id = "ds-agent-log-body";', 1)
# fix body element
text = text.replace(
    '    const body = document.createElement("motion");\n    body.id = "ds-agent-log-body";',
    '    const body = document.createElement("div");\n    body.id = "ds-agent-log-body";',
)

p.write_text(text, encoding="utf-8")
print("patched ok", "setWorkMode" in text)
