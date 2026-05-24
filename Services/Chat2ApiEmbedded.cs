namespace DeepSeekBrowser.Services;

/// <summary>
/// 内嵌 Chat2API（对齐 <see href="https://chat2api-doc.vercel.app/en/docs/"/>）。
/// <para>
/// 架构：<see cref="LocalOpenAiServer"/> 提供 OpenAI 兼容 HTTP API；
/// <see cref="WebChatBridgeHost"/> + <c>Assets/inject/bridge.js</c> 复用 Chat2API-main 的 DeepSeek 网页协议
///（PoW、流式、Token 鉴权）；管理台 UI 移植自 Chat2API-main（<see cref="Chat2ApiConsoleHost"/>），DeepSeek 主题样式。
/// </para>
/// </summary>
public static class Chat2ApiEmbedded
{
    public const string DocsUrl = "https://chat2api-doc.vercel.app/en/docs/";

    public const string ProviderId = "deepseek-web";
}
