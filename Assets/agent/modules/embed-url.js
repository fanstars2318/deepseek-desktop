/**
 * Embedded panel URL builder (single source for DSD API / settings iframes).
 * Loaded before agent-app.js.
 */
(function () {
  "use strict";

  function embeddedUiBuild() {
    const m = /[?&]build=(\d+)/.exec(location.search || "");
    return m ? m[1] : "0";
  }

  function embedUrl(path) {
    const sep = path.indexOf("?") >= 0 ? "&" : "?";
    return "https://ds-agent.local/" + path + sep + "build=" + embeddedUiBuild();
  }

  window.DsAgentEmbed = {
    embeddedUiBuild: embeddedUiBuild,
    embedUrl: embedUrl,
  };
})();
