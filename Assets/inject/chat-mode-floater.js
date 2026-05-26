/**
 * 官网普通对话页：右上角 Agent/普通 切换（独立于 overlay.js）。
 * 支持 about:blank → chat.deepseek.com SPA；可被 C# ExecuteScript 重复执行。
 */
(function () {
  "use strict";

  if (!window.__dsChatModeFloaterBootstrapped) {
    window.__dsChatModeFloaterBootstrapped = true;

    var ICON =
      '<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true">' +
      '<path d="M12 2a4 4 0 0 1 4 4v1h1a3 3 0 0 1 3 3v9a3 3 0 0 1-3 3H7a3 3 0 0 1-3-3V10a3 3 0 0 1 3-3h1V6a4 4 0 0 1 4-4z"/></svg>';

    var STYLE_ID = "ds-chat-mode-floater-style";

    function isChatHost() {
      return /chat\.deepseek\.com/i.test(location.hostname || "");
    }

    function isAuthPath() {
      var path = (location.pathname || "").toLowerCase();
      return /\/sign[-_]?in|\/login|\/register|\/auth\b/.test(path);
    }

    function injectStyle() {
      if (document.getElementById(STYLE_ID)) return;
      var s = document.createElement("style");
      s.id = STYLE_ID;
      s.textContent =
        "#ds-desktop-overlay-root{position:fixed!important;inset:0!important;pointer-events:none!important;z-index:2147483647!important}" +
        "#ds-agent-mode-float.ds-chat-mode-floater{position:fixed!important;top:10px!important;right:16px!important;left:auto!important;" +
        "z-index:2147483647!important;pointer-events:auto!important;display:inline-flex!important;align-items:center!important;" +
        "justify-content:center!important;gap:6px!important;height:34px!important;padding:0 14px!important;border-radius:9999px!important;" +
        "border:1px solid #e5e7eb!important;background:rgba(255,255,255,.97)!important;color:#374151!important;" +
        "font-size:13px!important;line-height:1!important;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif!important;" +
        "cursor:pointer!important;box-shadow:0 4px 16px rgba(0,0,0,.06)!important;visibility:visible!important;opacity:1!important;" +
        "transition:opacity .12s ease,transform .12s ease,border-color .12s ease,background .12s ease!important;" +
        "#ds-agent-mode-float.ds-switching{pointer-events:none!important;opacity:.72!important}" +
        "box-sizing:border-box!important;-webkit-app-region:no-drag!important}" +
        "#ds-agent-mode-float.ds-chat-mode-floater.ds-on{border-color:#4d6bfe!important;color:#4d6bfe!important;background:#eef2ff!important}" +
        "#ds-agent-mode-float .ds-mode-float-icon{display:inline-flex!important;align-items:center!important;width:16px!important;height:16px!important;color:#6b7280!important}" +
        "#ds-agent-mode-float.ds-on .ds-mode-float-icon{color:#4d6bfe!important}";
      var p = document.head || document.documentElement;
      if (p) p.appendChild(s);
    }

    function overlayHost() {
      var host = document.getElementById("ds-desktop-overlay-root");
      if (host && !host.isConnected) {
        host.remove();
        host = null;
      }
      if (!host && document.documentElement) {
        host = document.createElement("div");
        host.id = "ds-desktop-overlay-root";
        host.setAttribute("data-ds-desktop-overlay", "1");
        document.documentElement.appendChild(host);
      }
      return host;
    }

    function applyState(st) {
      var btn = document.getElementById("ds-agent-mode-float");
      if (!btn || !st) return;
      var label = document.getElementById("ds-agent-mode-float-label");
      if (label) label.textContent = st.label || (st.highlight ? "Agent" : "普通");
      btn.classList.toggle("ds-on", !!st.highlight);
      btn.title = st.title || "";
    }

    function mount() {
      if (!isChatHost() || isAuthPath() || !document.documentElement) return false;

      injectStyle();
      var host = overlayHost();
      if (!host) return false;

      var btn = document.getElementById("ds-agent-mode-float");
      if (!btn) {
        btn = document.createElement("button");
        btn.type = "button";
        btn.id = "ds-agent-mode-float";
        btn.className = "ds-mode-float ds-chat-mode-floater";
        btn.setAttribute("data-ds-workmode-floater", "1");
        btn.setAttribute("data-ds-floater", "1");
        btn.innerHTML =
          '<span class="ds-mode-float-icon" aria-hidden="true">' +
          ICON +
          '</span><span id="ds-agent-mode-float-label">普通</span>';
        if (window.DsWorkMode && window.DsWorkMode.markFloater) window.DsWorkMode.markFloater(btn);
        host.appendChild(btn);
      } else if (btn.parentElement !== host) {
        host.appendChild(btn);
      }

      btn.style.setProperty("display", "inline-flex", "important");
      btn.style.setProperty("visibility", "visible", "important");
      btn.style.setProperty("opacity", "1", "important");

      if (window.DsWorkMode && window.DsWorkMode.getState) {
        var st = window.DsWorkMode.getState();
        if (st) applyState(st);
      }
      window.__dsChatModeFloaterInstalled = true;
      return true;
    }

    window.__dsEnsureChatModeFloater = mount;
    window.__dsMountModeFloater = mount;

    function bindWorkMode() {
      if (window.__dsChatFloaterWmBound || !window.DsWorkMode) return;
      window.__dsChatFloaterWmBound = true;
      window.DsWorkMode.onChange(function (st) {
        applyState(st);
        mount();
      });
      if (window.DsWorkMode.flushPending) window.DsWorkMode.flushPending();
    }

    window.__dsChatModeFloaterBoot = function boot() {
      if (!isChatHost()) return;
      mount();
      bindWorkMode();
      if (!window.__dsChatModeFloaterMo && document.documentElement) {
        window.__dsChatModeFloaterMo = new MutationObserver(function () {
          if (isChatHost() && !document.getElementById("ds-agent-mode-float")) mount();
        });
        try {
          window.__dsChatModeFloaterMo.observe(document.documentElement, {
            childList: true,
            subtree: true,
          });
        } catch (_) {}
      }
    };

    if (document.readyState === "loading") {
      document.addEventListener("DOMContentLoaded", function () {
        window.__dsChatModeFloaterBoot();
      }, { once: true });
    }
    window.addEventListener("pageshow", function () {
      window.__dsChatModeFloaterBoot();
    });
    var lastHref = location.href;
    setInterval(function () {
      if (isChatHost()) {
        if (location.href !== lastHref) lastHref = location.href;
        window.__dsChatModeFloaterBoot();
      }
    }, 500);
  }

  if (window.__dsChatModeFloaterBoot) window.__dsChatModeFloaterBoot();
  setTimeout(function () {
    if (window.__dsChatModeFloaterBoot) window.__dsChatModeFloaterBoot();
  }, 0);
  setTimeout(function () {
    if (window.__dsChatModeFloaterBoot) window.__dsChatModeFloaterBoot();
  }, 300);
  setTimeout(function () {
    if (window.__dsChatModeFloaterBoot) window.__dsChatModeFloaterBoot();
  }, 1200);
})();
