#pragma once

#include <QJsonObject>
#include <QMainWindow>

class QStackedWidget;
class BridgeClient;
class WebViewPage;
class QProcess;

class MainWindow : public QMainWindow
{
    Q_OBJECT

public:
    explicit MainWindow(const QString &publishDir, QWidget *parent = nullptr);

private slots:
    void onBridgeEnvelope(const QString &channel, const QJsonObject &payload);
    void onBridgeDisconnected();
    void onBridgeProcessFinished(int exitCode, QProcess::ExitStatus status);

private:
    void startBridgeProcess();
    bool connectBridge();
    void showSurface(const QString &surface);

    QString m_publishDir;
    QStackedWidget *m_stack = nullptr;
    WebViewPage *m_chatPage = nullptr;
    WebViewPage *m_agentPage = nullptr;
    BridgeClient *m_bridge = nullptr;
    QProcess *m_bridgeProcess = nullptr;
};
