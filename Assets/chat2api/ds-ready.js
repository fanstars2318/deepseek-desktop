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

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", tick);
  } else {
    tick();
  }
})();
