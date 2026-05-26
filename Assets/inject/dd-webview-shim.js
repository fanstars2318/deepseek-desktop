(function () {
  "use strict";
  if (window.__dsDdShimInstalled) return;
  window.__dsDdShimInstalled = true;

  function deliverOutbound(s) {
    var payload = typeof s === "string" ? s : JSON.stringify(s);
    if (typeof window.__dsDdPostMessage === "function") {
      window.__dsDdPostMessage(payload);
      return;
    }
    window.__dsPendingDdOutbound = window.__dsPendingDdOutbound || [];
    window.__dsPendingDdOutbound.push(payload);
  }

  function flushDdOutbound() {
    if (typeof window.__dsDdPostMessage !== "function") return;
    var q = window.__dsPendingDdOutbound;
    if (!Array.isArray(q) || !q.length) return;
    window.__dsPendingDdOutbound = [];
    for (var i = 0; i < q.length; i++) {
      try {
        window.__dsDdPostMessage(q[i]);
      } catch (_) {}
    }
  }

  window.__dsFlushDdOutbound = flushDdOutbound;

  if (!(window.chrome && window.chrome.webview && window.chrome.webview.postMessage)) {
    window.chrome = window.chrome || {};
    window.chrome.webview = window.chrome.webview || {
      postMessage: deliverOutbound,
    };
  }

  if (typeof window.dsDesktopOnMessage !== "function") {
    window.dsDesktopOnMessage = function (msg) {
      try {
        if (window.__dsOnDesktopMessage) window.__dsOnDesktopMessage(msg);
      } catch (e) {
        console.warn("[ds] dsDesktopOnMessage", e);
      }
    };
  }
})();
