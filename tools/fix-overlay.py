import re

path = r"c:\Users\xiaow\Desktop\DSD\deepseek-edge\Assets\inject\overlay.js"
with open(path, "r", encoding="utf-8") as f:
    s = f.read()

new_toast = """  function showToast(title, lines) {
    ensureStyles();
    let wrap = document.getElementById("ds-toast-wrap");
    if (!wrap) {
      wrap = document.createElement("div");
      wrap.id = "ds-toast-wrap";
      wrap.style.cssText =
        "position:fixed;right:20px;bottom:24px;z-index:2147483647;max-width:320px;pointer-events:none";
      document.body.appendChild(wrap);
    }
    const toast = document.createElement("motionless");
    toast = document.createElement("div");
    toast.style.cssText =
      "pointer-events:auto;margin-top:8px;padding:10px 14px;background:#fff;border:1px solid #e5e7eb;border-radius:12px;box-shadow:0 8px 24px rgba(0,0,0,.12);font-size:13px;color:#374151";
    const headEl = document.createElement("div");
    headEl.style.cssText = "font-weight:600;color:#111827;margin-bottom:4px";
    headEl.textContent = title;
    toast.appendChild(headEl);
    (lines || []).filter(Boolean).forEach(function (line) {
      const row = document.createElement("div");
      row.style.marginTop = "2px";
      row.textContent = line;
      toast.appendChild(row);
    });
    wrap.appendChild(toast);
    while (wrap.children.length > 3) wrap.removeChild(wrap.firstChild);
    setTimeout(function () { toast.remove(); }, 5000);
  }"""

new_toast = new_toast.replace(
    'const toast = document.createElement("motionless");\n    toast = document.createElement("motionless");',
    'const toast = document.createElement("motionless");',
).replace('const toast = document.createElement("motionless");', 'const toast = document.createElement("div");')

new_card = """  function showProviderCard(info) {
    ensureStyles();
    document.getElementById("ds-provider-mask")?.remove();
    const mask = document.createElement("div");
    mask.id = "ds-provider-mask";
    mask.style.cssText =
      "position:fixed;inset:0;z-index:2147483646;display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,.4)";
    const card = document.createElement("div");
    card.style.cssText =
      "width:420px;max-width:calc(100vw - 32px);background:#fff;border-radius:16px;border:1px solid #e5e7eb;box-shadow:0 20px 50px rgba(0,0,0,.15);padding:20px;font-family:inherit";
    const titleRow = document.createElement("div");
    titleRow.style.cssText = "display:flex;justify-content:space-between;align-items:center;margin-bottom:12px";
    const h3 = document.createElement("div");
    h3.style.cssText = "font-size:16px;font-weight:600;color:#111827";
    h3.textContent = "DeepSeek · Chat2API";
    const dot = document.createElement("span");
    dot.style.cssText =
      "width:10px;height:10px;border-radius:50%;background:" + (info.loggedIn ? "#22c55e" : "#ef4444");
    titleRow.append(h3, dot);
    const desc = document.createElement("div");
    desc.style.cssText = "font-size:13px;color:#6b7280;line-height:1.5;margin-bottom:12px";
    desc.textContent = "网页 User Token 自动转为本地 OpenAI API，MCP 可操控本机作业。";
    const stats = document.createElement("div");
    stats.style.cssText = "font-size:12px;color:#374151;margin-bottom:12px;line-height:1.8";
    stats.textContent =
      "账户: " + (info.loggedIn ? "1/1 在线" : "未登录") + "  |  认证: User Token  |  API: " + (info.loggedIn ? "已启用" : "未就绪");
    const apiBox = document.createElement("div");
    apiBox.style.cssText =
      "background:#f7f8fa;border-radius:10px;padding:10px;font-size:12px;color:#4d6bfe;word-break:break-all;margin-bottom:14px";
    apiBox.textContent = info.url || apiUrl;
    const actions = document.createElement("div");
    actions.style.cssText = "display:flex;justify-content:flex-end;gap:8px";
    const btnSettings = document.createElement("button");
    btnSettings.type = "button";
    btnSettings.textContent = "MCP 设置";
    btnSettings.style.cssText = "padding:8px 14px;border-radius:10px;border:1px solid #e5e7eb;background:#fff;cursor:pointer;font-size:13px";
    const btnClose = document.createElement("button");
    btnClose.type = "button";
    btnClose.textContent = "确定";
    btnClose.style.cssText =
      "padding:8px 14px;border-radius:10px;border:none;background:#4d6bfe;color:#fff;cursor:pointer;font-size:13px";
    actions.append(btnSettings, btnClose);
    card.append(titleRow, desc, stats, apiBox, actions);
    mask.appendChild(card);
    document.body.appendChild(mask);
    mask.addEventListener("click", function (e) { if (e.target === mask) mask.remove(); });
    btnClose.onclick = function () { mask.remove(); };
    btnSettings.onclick = function () { mask.remove(); post("openSettings", {}); };
  }"""

new_card = new_card.replace(
    'const h3 = document.createElement("motionless");\n    h3 = document.createElement("motionless");',
    "",
).replace('h3 = document.createElement("motionless");', 'const h3 = document.createElement("div");')

pattern = r"  function showToast\(title, lines\) \{[\s\S]*?  function updateApiDot\(\)"
s2, n = re.subn(pattern, new_toast + "\n\n" + new_card + "\n\n  function updateApiDot()", s, count=1)
if n != 1:
    raise SystemExit(f"replace failed n={n}")

with open(path, "w", encoding="utf-8", newline="\n") as f:
    f.write(s2)
print("ok")
