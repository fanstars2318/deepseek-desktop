(function () {
  "use strict";
  if (window.__dsConsoleRebrand) return;
  window.__dsConsoleRebrand = true;

  var BRAND = "DeepSeek";
  var TITLE = "API 管理";
  var SUBTITLE = "多厂商模型与密钥";

  function applyHeaderBrand() {
    document.title = TITLE;
    var topbar = document.querySelector(".glass-topbar");
    if (!topbar) return false;
    var title = topbar.querySelector(".font-bold");
    if (title) {
      title.textContent = BRAND;
      title.classList.add("ds-console-brand-title");
    }
    var subtitle = topbar.querySelector(".text-xs, .text-sm");
    if (subtitle && /DSD API|API|Multi-platform/i.test(subtitle.textContent || "")) {
      subtitle.textContent = SUBTITLE;
    }
    document.querySelectorAll(".font-bold, h1, span").forEach(function (el) {
      var t = (el.textContent || "").trim();
      if (t === "DSD API" || t === "Chat2API" || t === "Chat2Api") el.textContent = TITLE;
    });
    var logoImg = topbar.querySelector(".sidebar-logo-icon img");
    if (logoImg) {
      logoImg.alt = "DeepSeek";
      logoImg.style.display = "none";
    }
    var logoWrap = topbar.querySelector(".sidebar-logo-icon");
    if (logoWrap && !logoWrap.querySelector(".ds-console-whale")) {
      logoWrap.classList.add("ds-console-whale-wrap");
      var whale = document.createElement("span");
      whale.className = "ds-console-whale";
      whale.setAttribute("aria-hidden", "true");
      logoWrap.insertBefore(whale, logoWrap.firstChild);
    }
    return true;
  }

  function scheduleBrand() {
    if (applyHeaderBrand()) return;
    var left = 8;
    function tick() {
      if (applyHeaderBrand() || --left <= 0) return;
      requestAnimationFrame(function () {
        setTimeout(tick, 200);
      });
    }
    tick();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", scheduleBrand);
  } else {
    scheduleBrand();
  }
  window.addEventListener("load", scheduleBrand);
})();
