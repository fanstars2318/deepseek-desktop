#pragma once

#include <QWidget>

class QWebEngineView;
class QWebChannel;
class DeepSeekHost;
class BridgeClient;

class WebViewPage : public QWidget
{
    Q_OBJECT

public:
    explicit WebViewPage(BridgeClient *bridge, const QString &channel, const QString &assetsRoot,
                         bool injectChatOverlay, QWidget *parent = nullptr);

    QWebEngineView *view() const { return m_view; }

    void deliverPayload(const QJsonObject &payload);

public slots:
    void loadUrl(const QUrl &url);

private:
    void setupChannel();
    void injectCreationScripts();

    QWebEngineView *m_view = nullptr;
    QWebChannel *m_channel = nullptr;
    DeepSeekHost *m_host = nullptr;
    BridgeClient *m_bridge = nullptr;
    QString m_channel;
    QString m_assetsRoot;
    bool m_injectChatOverlay = false;
};
