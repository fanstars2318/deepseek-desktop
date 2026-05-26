(function () {
  "use strict";
  if (window.__dsSettingsLoadBalance) return;
  window.__dsSettingsLoadBalance = true;

  var STRATEGIES = [
    { id: "round-robin", label: "轮询", desc: "按权重在账户间轮询分配请求。" },
    { id: "fill-first", label: "填充优先", desc: "优先使用当前账户直到达到限制，然后切换到下一个账户。" },
    { id: "failover", label: "故障转移", desc: "主账户失败时自动切换到备用账户。" },
  ];

  function t(key, fallback) {
    try {
      if (window.i18next && typeof window.i18next.t === "function") {
        var v = window.i18next.t(key);
        if (v && v !== key) return v;
      }
    } catch (_) {}
    return fallback;
  }

  function invoke(channel, args) {
    if (!window.electronAPI || !window.electronAPI.invoke) {
      return Promise.reject(new Error("electronAPI unavailable"));
    }
    return window.electronAPI.invoke(channel, args);
  }

  function getConfig() {
    if (window.electronAPI?.config?.get) return window.electronAPI.config.get();
    return invoke("config:get");
  }

  function updateConfig(patch) {
    if (window.electronAPI?.config?.update) return window.electronAPI.config.update(patch);
    return invoke("config:update", patch);
  }

  function el(tag, className, text) {
    var node = document.createElement(tag);
    if (className) node.className = className;
    if (text != null) node.textContent = text;
    return node;
  }

  function renderPanel(root, config, accounts) {
    root.replaceChildren();
    var strategy = config.loadBalanceStrategy || "round-robin";
    var weights = Array.isArray(config.accountWeights) ? config.accountWeights : [];
    var weightMap = {};
    weights.forEach(function (w) {
      if (w && w.accountId) weightMap[w.accountId] = w.weight;
    });

    var card = el("div", "glass-card p-6 space-y-6");
    var head = el("div", "space-y-1");
    head.appendChild(el("h3", "text-lg font-semibold", t("proxy.loadBalanceConfig", "负载均衡配置")));
    head.appendChild(
      el(
        "p",
        "text-sm text-muted-foreground",
        t("proxy.loadBalanceConfigDesc", "配置账户选择策略和权重分配")
      )
    );
    card.appendChild(head);

    var stratBlock = el("div", "space-y-2");
    stratBlock.appendChild(el("label", "text-sm font-medium", t("proxy.loadBalanceStrategy", "负载均衡策略")));
    var select = el("select", "flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm");
    STRATEGIES.forEach(function (s) {
      var opt = el("option", "", s.label);
      opt.value = s.id;
      if (strategy === s.id) opt.selected = true;
      select.appendChild(opt);
    });
    stratBlock.appendChild(select);
    var hint = el(
      "p",
      "text-sm text-muted-foreground p-3 rounded-lg bg-muted/50",
      STRATEGIES.find(function (s) {
        return s.id === strategy;
      })?.desc || ""
    );
    select.addEventListener("change", function () {
      var picked = STRATEGIES.find(function (s) {
        return s.id === select.value;
      });
      if (picked) hint.textContent = picked.desc;
    });
    stratBlock.appendChild(hint);
    card.appendChild(stratBlock);

    var active = accounts.filter(function (a) {
      return a.status === "active";
    });
    if (active.length > 0) {
      var weightsTitle = el("div", "flex items-center justify-between pt-2 border-t border-[var(--glass-border)]");
      weightsTitle.appendChild(el("span", "text-sm font-medium", t("proxy.accountWeightConfig", "账户权重配置")));
      card.appendChild(weightsTitle);

      var list = el("div", "space-y-4");
      active.forEach(function (account) {
        var row = el("div", "space-y-2");
        var labelRow = el("div", "flex items-center justify-between text-sm");
        var name = (account.name || account.id) + (account.provider ? " · " + account.provider.name : "");
        labelRow.appendChild(el("span", "font-medium", name));
        var pct = el("span", "text-muted-foreground ds-lb-pct", String(weightMap[account.id] ?? 100) + "%");
        labelRow.appendChild(pct);
        row.appendChild(labelRow);

        var range = el("input", "ds-lb-range w-full accent-[var(--accent-primary,#22c55e)]");
        range.type = "range";
        range.min = "0";
        range.max = "100";
        range.value = String(weightMap[account.id] ?? 100);
        range.dataset.accountId = account.id;
        range.addEventListener("input", function () {
          pct.textContent = range.value + "%";
        });
        row.appendChild(range);
        list.appendChild(row);
      });
      card.appendChild(list);
      card.appendChild(
        el(
          "p",
          "text-xs text-muted-foreground",
          t(
            "proxy.weightHint",
            "权重越高，该账户被选中的概率越大。仅在轮询策略下生效。"
          )
        )
      );
    } else {
      card.appendChild(
        el("p", "text-sm text-muted-foreground", "暂无活跃账户，请先在供应商页配置账户。")
      );
    }

    var actions = el("div", "flex gap-2 justify-end");
    var saveBtn = el("button", "ds-btn ds-btn-primary", t("common.save", "保存"));
    var resetBtn = el("button", "ds-btn ds-btn-secondary", t("common.reset", "重置"));
    actions.appendChild(resetBtn);
    actions.appendChild(saveBtn);
    card.appendChild(actions);

    root.appendChild(card);

    var initialStrategy = strategy;
    var initialWeights = JSON.stringify(weights);

    resetBtn.addEventListener("click", function () {
      void bootstrap();
    });

    saveBtn.addEventListener("click", function () {
      saveBtn.disabled = true;
      var nextWeights = [];
      root.querySelectorAll(".ds-lb-range").forEach(function (input) {
        nextWeights.push({
          accountId: input.dataset.accountId,
          weight: parseInt(input.value, 10) || 0,
        });
      });
      updateConfig({
        loadBalanceStrategy: select.value,
        accountWeights: nextWeights,
      })
        .then(function () {
          initialStrategy = select.value;
          initialWeights = JSON.stringify(nextWeights);
        })
        .catch(function (err) {
          console.error("[ds-settings-loadbalance]", err);
          alert("保存失败：" + (err && err.message ? err.message : String(err)));
        })
        .finally(function () {
          saveBtn.disabled = false;
        });
    });
  }

  function bootstrap() {
    var root = document.getElementById("ds-loadbalance-root");
    if (!root) return;
    Promise.all([
      getConfig(),
      window.electronAPI?.accounts?.getAll?.() || Promise.resolve([]),
      window.electronAPI?.providers?.getAll?.() || Promise.resolve([]),
    ])
      .then(function (arr) {
        var config = arr[0] || {};
        var accounts = arr[1] || [];
        var providers = arr[2] || [];
        var withProvider = accounts.map(function (account) {
          return {
            ...account,
            provider: providers.find(function (p) {
              return p.id === account.providerId;
            }),
          };
        });
        renderPanel(root, config, withProvider);
      })
      .catch(function (err) {
        root.textContent = "加载负载均衡配置失败";
        console.error("[ds-settings-loadbalance]", err);
      });
  }

  function ensureTab() {
    var lists = document.querySelectorAll("[role='tablist']");
    lists.forEach(function (list) {
      if (list.querySelector('[data-value="loadbalance"], [value="loadbalance"]')) return;
      var mgmt = list.querySelector('[value="managementApi"]');
      if (!mgmt) return;
      mgmt.setAttribute("value", "loadbalance");
      mgmt.setAttribute("data-value", "loadbalance");
      var label = mgmt.querySelector("span.hidden.sm\\:inline, span");
      if (label) label.textContent = "负载均衡";
      list.classList.remove("grid-cols-5");
      list.classList.add("grid-cols-5");
    });

    var panel = document.querySelector('[value="managementApi"][role="tabpanel"], [data-value="managementApi"]');
    if (panel) {
      panel.setAttribute("value", "loadbalance");
      panel.setAttribute("data-value", "loadbalance");
      panel.innerHTML = '<div id="ds-loadbalance-root" class="space-y-4"></div>';
    }
  }

  function tick() {
    if ((location.hash || "").indexOf("#/settings") < 0) return;
    ensureTab();
    bootstrap();
  }

  var left = 30;
  (function run() {
    tick();
    if (--left > 0) setTimeout(run, 400);
  })();

  window.addEventListener("hashchange", function () {
    setTimeout(tick, 0);
  });

  var rootEl = document.getElementById("root");
  if (rootEl) {
    new MutationObserver(function () {
      tick();
    }).observe(rootEl, { childList: true, subtree: true });
  }
})();
