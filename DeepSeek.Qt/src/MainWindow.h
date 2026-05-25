#pragma once

#include <QJsonObject>
#include <QMainWindow>
#include <QProcess>
#include <QResizeEvent>

class QStackedWidget;
class BridgeClient;
class WebViewPage;
class QWidget;
class QLabel;

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
    void setLoadingVisible(bool visible);
    void resizeEvent(QResizeEvent *event) override;

    QString m_publishDir;
    QStackedWidget *m_stack = nullptr;
    QWidget *m_loadingOverlay = nullptr;
    WebViewPage *m_chatPage = nullptr;
    WebViewPage *m_agentPage = nullptr;
    BridgeClient *m_bridge = nullptr;
    QProcess *m_bridgeProcess = nullptr;
};
