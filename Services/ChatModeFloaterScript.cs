namespace DeepSeekBrowser.Services;

/// <summary>在 chat.deepseek.com 上强制挂载模式切换浮钮（与 document-created 脚本配合）。</summary>
public static class ChatModeFloaterScript
{
    /// <summary>最小挂载（不依赖外部 JS 文件；CSP 下 script 标签加载 ds-inject.local 会失败）。</summary>
    public const string MinimalMount =
        "(function(){try{"
        + "if(!/chat\\.deepseek\\.com/i.test(location.hostname||''))return;"
        + "window.__dsChatModeFloaterBootstrapped=true;"
        + "var host=document.getElementById('ds-desktop-overlay-root');"
        + "if(!host){host=document.createElement('div');host.id='ds-desktop-overlay-root';"
        + "host.style.cssText='position:fixed;inset:0;pointer-events:none;z-index:2147483647';"
        + "document.documentElement.appendChild(host);}"
        + "var b=document.getElementById('ds-agent-mode-float');"
        + "if(!b){b=document.createElement('button');b.type='button';b.id='ds-agent-mode-float';"
        + "b.className='ds-mode-float ds-chat-mode-floater';"
        + "b.innerHTML='<span id=\"ds-agent-mode-float-label\">普通</span>';"
        + "b.style.cssText='position:fixed;top:10px;right:16px;z-index:2147483647;display:inline-flex;"
        + "align-items:center;height:34px;padding:0 14px;border-radius:9999px;border:1px solid #e5e7eb;"
        + "background:rgba(255,255,255,.97);cursor:pointer;pointer-events:auto;font:13px sans-serif;color:#374151';"
        + "b.addEventListener('click',function(e){e.preventDefault();e.stopPropagation();"
        + "if(window.DsWorkMode&&window.DsWorkMode.activateFloater){window.DsWorkMode.activateFloater();return;}"
        + "if(window.chrome&&window.chrome.webview)"
        + "window.chrome.webview.postMessage(JSON.stringify({type:'toggleWorkMode'}));},true);"
        + "host.appendChild(b);}"
        + "b.style.setProperty('display','inline-flex','important');"
        + "window.__dsEnsureChatModeFloater=function(){"
        + "var x=document.getElementById('ds-agent-mode-float');if(x)x.style.setProperty('display','inline-flex','important');};"
        + "}catch(e){console.warn('[DeepSeek Desktop] MinimalMount',e);}})();";

    public const string LoadFromVirtualHost =
        "(function(){try{"
        + "if(!/chat\\.deepseek\\.com/i.test(location.hostname||''))return;"
        + "if(window.__dsChatModeFloaterBootstrapped)return;"
        + "var s=document.createElement('script');"
        + "s.src='https://ds-inject.local/chat-mode-floater.js';"
        + "s.onload=function(){if(window.__dsChatModeFloaterBoot)window.__dsChatModeFloaterBoot();"
        + "if(window.__dsEnsureChatModeFloater)window.__dsEnsureChatModeFloater();};"
        + "(document.head||document.documentElement).appendChild(s);"
        + "}catch(e){console.warn('[DeepSeek Desktop] load chat-mode-floater',e);}})();";

    public const string Ensure =
        "(function(){try{"
        + "if(!/chat\\.deepseek\\.com/i.test(location.hostname||''))return;"
        + "if(window.__dsEnsureChatModeFloater)window.__dsEnsureChatModeFloater();"
        + "else if(window.__dsMountModeFloater)window.__dsMountModeFloater();"
        + "}catch(e){console.warn('[DeepSeek Desktop] EnsureChatModeFloater',e);}})();";

    /// <summary>自检：返回 JSON { ok, boot, url, width, height }。</summary>
    public const string Probe =
        "(function(){try{"
        + "if(window.__dsEnsureChatModeFloater)window.__dsEnsureChatModeFloater();"
        + "var b=document.getElementById('ds-agent-mode-float');"
        + "if(!b)return JSON.stringify({ok:false,reason:'no-button',boot:!!window.__dsChatModeFloaterBootstrapped,url:location.href});"
        + "var r=b.getBoundingClientRect();"
        + "var st=window.getComputedStyle(b);"
        + "return JSON.stringify({ok:r.width>0&&r.height>0&&st.display!=='none'&&st.visibility!=='hidden',"
        + "boot:!!window.__dsChatModeFloaterBootstrapped,url:location.href,width:r.width,height:r.height,display:st.display});"
        + "}catch(e){return JSON.stringify({ok:false,error:String(e)});}})();";
}
