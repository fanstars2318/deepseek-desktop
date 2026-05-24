(function () {
  "use strict";
  if (window.__dsConsoleRebrand) return;
  window.__dsConsoleRebrand = true;

  var BRAND = "deepseek";
  var TITLE = "DeepSeek API 管理";

  function applyHeaderBrand() {
    document.title = TITLE;
    var topbar = document.querySelector(".glass-topbar");
    if (!topbar) return false;
    var title = topbar.querySelector(".font-bold");
    if (title) {
      title.textContent = BRAND;
      title.classList.add("ds-console-brand-title");
    }
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
