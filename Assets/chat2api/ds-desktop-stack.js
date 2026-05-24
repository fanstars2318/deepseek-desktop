(function () {
  "use strict";
  if (window.__dsDesktopStack) return;
  window.__dsDesktopStack = true;

  function postDesktop(type, payload) {
    var body = JSON.stringify(
      Object.assign({ type: type, __dsEmbed: true }, payload || {})
    );
    if (window.parent && window.parent !== window) {
      window.parent.postMessage(body, "*");
      return;
    }
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(body);
    }
  }

  function el(tag, cls, text) {
    var node = document.createElement(tag);
    if (cls) node.className = cls;
    if (text) node.textContent = text;
    return node;
  }

  function renderStack(info) {
    var bar = document.getElementById("ds-desktop-stack-bar");
    if (!bar) return;
    bar.innerHTML = "";

    var title = el("div", "ds-stack-title", "DeepSeek 桌面栈");
    bar.appendChild(title);

    var grid = el("div", "ds-stack-grid");
    var rows = [
      ["网页会话", info.loggedIn ? "已登录" : "未登录"],
      ["内嵌通道", info.internalChannel || "internal://desktop/v1"],
      ["DeepSeek-TUI", info.tuiRuntimeUrl || ""],
      ["TUI 配置", info.tuiConfigPath || ""],
      ["会话模式", info.sessionMode === "multi" ? "多轮" : "单轮"],
      ["Agent 模式", info.defaultWorkMode || "chat"],
      ["深度思考", info.agentDeepThinking ? "开" : "关"],
      ["联网搜索", info.agentWebSearch ? "开" : "关"],
      ["外部 API", info.externalApiEnabled ? info.externalApiBaseUrl || "已启用" : "未启用"],
      ["模型映射", String(info.modelMappingCount || 0) + " 条"]
    ];

    rows.forEach(function (row) {
      var item = el("div", "ds-stack-row");
      item.appendChild(el("span", "ds-stack-label", row[0]));
      item.appendChild(el("span", "ds-stack-value", row[1]));
      grid.appendChild(item);
    });
    bar.appendChild(grid);

    if (!info.loggedIn && info.loginHint) {
      bar.appendChild(el("p", "ds-stack-hint", info.loginHint));
    }

    var actions = el("div", "ds-stack-actions");
    [
      { id: "login", label: "打开主窗口登录", type: "openDeepSeekLogin" },
      { id: "sync", label: "同步到 TUI", type: "syncDesktopStack" },
      { id: "tui", label: "打开 TUI 配置", type: "openTuiConfigFile" },
      { id: "agent", label: "Agent 面板", type: "openAgentFromChat2Api" }
    ].forEach(function (act) {
      var btn = el("button", "ds-stack-btn", act.label);
      btn.type = "button";
      btn.setAttribute("data-ds-action", act.id);
      btn.addEventListener("click", function () {
        postDesktop(act.type);
      });
      actions.appendChild(btn);
    });
    bar.appendChild(actions);
  }

  async function refreshStack() {
    try {
      if (!window.electronAPI?.config?.get) return;
      var cfg = await window.electronAPI.config.get();
      if (cfg && cfg.deepseekDesktop) renderStack(cfg.deepseekDesktop);
    } catch (_) {}
  }

  function ensureBar() {
    if (document.getElementById("ds-desktop-stack-bar")) return;
    var topbar = document.querySelector(".glass-topbar");
    if (!topbar) return;
    var bar = el("div", "ds-desktop-stack-bar");
    bar.id = "ds-desktop-stack-bar";
    topbar.parentElement.insertBefore(bar, topbar.nextSibling);
  }

  function schedule() {
    ensureBar();
    refreshStack();
    var left = 30;
    function tick() {
      ensureBar();
      if (--left > 0) setTimeout(tick, 1000);
    }
    tick();
  }

  window.addEventListener("message", function (e) {
    try {
      var msg = typeof e.data === "string" ? JSON.parse(e.data) : e.data;
      if (msg && msg.type === "desktopStackSynced") refreshStack();
    } catch (_) {}
  });

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", schedule);
  } else {
    schedule();
  }
  window.addEventListener("load", schedule);
})();
