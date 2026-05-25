#include "WebViewPage.h"

#include "BridgeClient.h"
#include "DeepSeekHost.h"

#include <QFile>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonValue>
#include <QVBoxLayout>
#include <QWebChannel>
#include <QWebEnginePage>
#include <QWebEngineProfile>
#include <QWebEngineScript>
#include <QWebEngineScriptCollection>
#include <QWebEngineSettings>
#include <QWebEngineView>

namespace {

QString readTextFile(const QString &path)
{
    QFile f(path);
    if (!f.open(QIODevice::ReadOnly))
        return {};
    return QString::fromUtf8(f.readAll());
}

QString escapeForJs(const QString &s)
{
    return QString::fromUtf8(QJsonDocument::fromVariant(s).toJson(QJsonDocument::Compact));
}

} // namespace

WebViewPage::WebViewPage(BridgeClient *bridge, const QString &channel, const QString &assetsRoot,
                         bool injectChatOverlay, QWidget *parent)
    : QWidget(parent)
    , m_bridge(bridge)
    , m_ipcChannel(channel)
    , m_assetsRoot(assetsRoot)
    , m_injectChatOverlay(injectChatOverlay)
{
    auto *layout = new QVBoxLayout(this);
    layout->setContentsMargins(0, 0, 0, 0);

    m_view = new QWebEngineView(this);
    m_view->settings()->setAttribute(QWebEngineSettings::FocusOnNavigationEnabled, true);
    m_view->settings()->setAttribute(QWebEngineSettings::JavascriptEnabled, true);
    layout->addWidget(m_view);

    setupChannel();
    injectCreationScripts();
}

void WebViewPage::setupChannel()
{
    m_host = new DeepSeekHost(m_bridge, m_ipcChannel, this);
    m_webChannel = new QWebChannel(m_view->page());
    m_webChannel->registerObject(QStringLiteral("deepSeekHost"), m_host);
    m_view->page()->setWebChannel(m_webChannel);

    connect(m_host, &DeepSeekHost::sendMessage, this, [this](const QString &json) {
        QJsonParseError err;
        const QJsonDocument doc = QJsonDocument::fromJson(json.toUtf8(), &err);
        if (err.error == QJsonParseError::NoError && doc.isObject())
            deliverPayload(doc.object());
    });
}

void WebViewPage::injectCreationScripts()
{
    const QString injectDir = m_assetsRoot + QStringLiteral("/inject");
    auto &scripts = m_view->page()->scripts();

    const auto addScript = [&](const QString &source) {
        if (source.isEmpty())
            return;
        QWebEngineScript script;
        script.setSourceCode(source);
        script.setInjectionPoint(QWebEngineScript::DocumentCreation);
        script.setWorldId(QWebEngineScript::MainWorld);
        script.setRunsOnSubFrames(true);
        scripts.insert(script);
    };

    addScript(readTextFile(injectDir + QStringLiteral("/dd-webview-shim.js")));
    addScript(readTextFile(injectDir + QStringLiteral("/qwebchannel.js")));

    const QString channelBootstrap = QString::fromLatin1(
        "(function(){"
        "if(typeof qt==='undefined'||!qt.webChannelTransport)return;"
        "new QWebChannel(qt.webChannelTransport,function(ch){"
        "var h=ch.objects.deepSeekHost;"
        "if(!h)return;"
        "window.__dsDdPostMessage=function(s){h.receiveMessage(s);};"
        "if(h.sendMessage&&h.sendMessage.connect){"
        "h.sendMessage.connect(function(j){"
        "try{var m=typeof j==='string'?JSON.parse(j):j;"
        "if(window.dsDesktopOnMessage)window.dsDesktopOnMessage(m);"
        "}catch(e){console.warn('[ds] qt sendMessage',e);}"
        "});}"
        "});"
        "})();");

    addScript(channelBootstrap);
    addScript(readTextFile(injectDir + QStringLiteral("/bridge.js")));
    addScript(readTextFile(injectDir + QStringLiteral("/work-mode-client.js")));

    if (m_injectChatOverlay) {
        const QString overlayJs = readTextFile(injectDir + QStringLiteral("/overlay.js"));
        const QString guarded = QString::fromLatin1(
                                    "if(!/^ds-agent\\.local$/i.test(location.hostname)){") +
                                overlayJs +
                                QStringLiteral("}");
        addScript(guarded);

        const QString overlayCss = readTextFile(injectDir + QStringLiteral("/overlay.css"));
        if (!overlayCss.isEmpty()) {
            const QString esc = escapeForJs(overlayCss);
            addScript(QString::fromLatin1(
                "(function(){try{"
                "if(/^ds-agent\\.local$/i.test(location.hostname))return;"
                "var p=document.head||document.documentElement;if(!p)return;"
                "var s=document.createElement('style');s.textContent=") +
                esc +
                QString::fromLatin1(";p.appendChild(s);}catch(e){}})();"));
        }
    }
}

void WebViewPage::loadUrl(const QUrl &url)
{
    m_view->load(url);
}

void WebViewPage::deliverPayload(const QJsonObject &payload)
{
    const QByteArray json = QJsonDocument(payload).toJson(QJsonDocument::Compact);
    const QString expr = QString::fromLatin1(
                             "try{if(window.dsDesktopOnMessage)window.dsDesktopOnMessage(") +
                         QString::fromUtf8(json) +
                         QString::fromLatin1(");}catch(e){console.warn('[ds] deliver',e);}");
    m_view->page()->runJavaScript(expr);
}
