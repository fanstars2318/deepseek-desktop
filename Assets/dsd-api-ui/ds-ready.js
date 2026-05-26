(function () {
  "use strict";
  if (window.__dsConsoleReadyWatch) return;
  window.__dsConsoleReadyWatch = true;

  try {
    localStorage.setItem("i18nextLng", "zh-CN");
  } catch (_) {}

  function postReady() {
    if (window.__dsConsoleReadyPosted) return;
    window.__dsConsoleReadyPosted = true;
    try {
      var body = JSON.stringify({ type: "consoleUiReady", __dsEmbed: true });
      if (window.parent && window.parent !== window) {
        window.parent.postMessage(body, "*");
      } else if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(body);
      }
    } catch (_) {}
  }

  function isPainted() {
    var root = document.getElementById("root");
    if (!root || root.childElementCount === 0) return false;
  return !!(
      document.querySelector(".glass-topbar") ||
      document.querySelector(".glass-sidebar") ||
      document.querySelector("main")
    );
  }

  var left = 400;
  function tick() {
    if (isPainted()) {
      requestAnimationFrame(postReady);
      return;
    }
    if (--left <= 0) {
      postReady();
      return;
    }
    setTimeout(tick, 50);
  }

  function parseMessage(data) {
    if (data == null) return null;
    if (typeof data === "object") return data;
    if (typeof data === "string") {
      try {
        return JSON.parse(data);
      } catch (_) {
        return null;
      }
    }
    return null;
  }

  function isProvidersStuckLoading() {
    try {
      var hash = location.hash || "";
      if (hash.indexOf("#/providers") < 0) return false;
      var main = document.querySelector("main");
      if (!main) return false;
      var text = (main.textContent || "").replace(/\s+/g, " ");
      if (!/加载|loading/i.test(text)) return false;
      return main.querySelectorAll("[class*='card'], [data-provider-id], article").length === 0;
    } catch (_) {
      return false;
    }
  }

  function remountProvidersRoute() {
    try {
      location.hash = "#/models";
      setTimeout(function () {
        location.hash = "#/providers";
      }, 60);
    } catch (_) {}
  }

  window.addEventListener("message", function (e) {
    var msg = parseMessage(e.data);
    if (!msg || msg.type !== "embeddedPanelOpen") return;
    if (!isProvidersStuckLoading()) return;
    remountProvidersRoute();
  });

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", tick);
  } else {
    tick();
  }
})();
