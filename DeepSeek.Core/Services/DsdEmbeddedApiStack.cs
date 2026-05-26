namespace DeepSeekBrowser.Services;

/// <summary>
/// DeepSeek Desktop（DPDT）内嵌 OpenAI 兼容 API 栈。
/// <para>
/// 架构：<see cref="LocalOpenAiServer"/> 提供 OpenAI 兼容 HTTP API；
/// <see cref="WebChatBridgeHost"/> + <c>Assets/inject/bridge.js</c> 复用 DeepSeek 网页协议
///（PoW、流式、Token 鉴权）；管理台 UI 位于 <c>Assets/dsd-api</c>，DeepSeek Desktop 主题样式。
/// </para>
/// </summary>
public static class DsdEmbeddedApiStack
{
    public const string DocsUrl = "https://api-docs.deepseek.com/";

    public const string ProviderId = "deepseek-web";
}
