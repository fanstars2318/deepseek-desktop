(function () {
  "use strict";
  if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) return;

  window.chrome = window.chrome || {};
  window.chrome.webview = window.chrome.webview || {
    postMessage: function (s) {
      if (typeof window.__dsDdPostMessage === "function") {
        window.__dsDdPostMessage(typeof s === "string" ? s : JSON.stringify(s));
      }
    },
  };

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
