#include "MainWindow.h"

#include "BridgeClient.h"
#include "WebViewPage.h"

#include <QApplication>
#include <QDir>
#include <QFileInfo>
#include <QIcon>
#include <QJsonObject>
#include <QProcess>
#include <QStackedWidget>
#include <QTimer>
#include <QWebEngineProfile>
#include <QWebEngineView>

#include "AssetUrlScheme.h"

MainWindow::MainWindow(const QString &publishDir, QWidget *parent)
    : QMainWindow(parent)
    , m_publishDir(publishDir)
{
    setWindowTitle(QStringLiteral("DeepSeek"));
    setMinimumSize(900, 600);
    resize(1280, 800);

    const QString iconPath = QDir(publishDir).filePath(QStringLiteral("Assets/deepseek.ico"));
    if (QFileInfo::exists(iconPath))
        setWindowIcon(QIcon(iconPath));

    const QString assetsRoot = QDir(publishDir).filePath(QStringLiteral("Assets"));
    AssetUrlScheme::installHandlers(QWebEngineProfile::defaultProfile(), assetsRoot);

    m_stack = new QStackedWidget(this);
    setCentralWidget(m_stack);

    m_bridge = new BridgeClient(this);
    connect(m_bridge, &BridgeClient::envelopeReceived, this, &MainWindow::onBridgeEnvelope);
    connect(m_bridge, &BridgeClient::disconnected, this, &MainWindow::onBridgeDisconnected);

    m_chatPage = new WebViewPage(m_bridge, QStringLiteral("chat"), assetsRoot, true, this);
    m_agentPage = new WebViewPage(m_bridge, QStringLiteral("agent"), assetsRoot, false, this);
    m_stack->addWidget(m_chatPage);
    m_stack->addWidget(m_agentPage);

    m_agentPage->loadUrl(QUrl(QStringLiteral("https://ds-agent.local/index.html?build=23")));
    m_chatPage->loadUrl(QUrl(QStringLiteral("https://chat.deepseek.com/")));

    startBridgeProcess();
}

void MainWindow::startBridgeProcess()
{
    const QString bridgeExe = QDir(m_publishDir).filePath(QStringLiteral("DeepSeek.Bridge.exe"));
    if (!QFileInfo::exists(bridgeExe)) {
        qWarning("DeepSeek.Bridge.exe not found at %s", qPrintable(bridgeExe));
        return;
    }

    m_bridgeProcess = new QProcess(this);
    m_bridgeProcess->setWorkingDirectory(m_publishDir);
    connect(m_bridgeProcess, QOverload<int, QProcess::ExitStatus>::of(&QProcess::finished), this,
            &MainWindow::onBridgeProcessFinished);
    m_bridgeProcess->start(bridgeExe, {});

    QTimer::singleShot(400, this, [this]() {
        if (!connectBridge())
            QTimer::singleShot(800, this, [this]() { connectBridge(); });
    });
}

bool MainWindow::connectBridge()
{
    if (!m_bridge->connectToBridge(20000))
        return false;

    m_bridge->sendControl(QJsonObject{
        {QStringLiteral("type"), QStringLiteral("ddReady")},
    });
    return true;
}

void MainWindow::onBridgeEnvelope(const QString &channel, const QJsonObject &payload)
{
    if (channel == QStringLiteral("control")) {
        const auto type = payload.value(QStringLiteral("type")).toString();
        if (type == QStringLiteral("ddSurface")) {
            showSurface(payload.value(QStringLiteral("surface")).toString());
        }
        return;
    }

    if (channel == QStringLiteral("agent"))
        m_agentPage->deliverPayload(payload);
    else if (channel == QStringLiteral("chat"))
        m_chatPage->deliverPayload(payload);
}

void MainWindow::showSurface(const QString &surface)
{
    if (surface == QStringLiteral("agent"))
        m_stack->setCurrentWidget(m_agentPage);
    else
        m_stack->setCurrentWidget(m_chatPage);
}

void MainWindow::onBridgeDisconnected()
{
    QTimer::singleShot(1500, this, [this]() {
        if (m_bridgeProcess && m_bridgeProcess->state() == QProcess::Running)
            connectBridge();
    });
}

void MainWindow::onBridgeProcessFinished(int exitCode, QProcess::ExitStatus status)
{
    Q_UNUSED(status);
    if (exitCode != 0)
        QTimer::singleShot(2000, this, &MainWindow::startBridgeProcess);
}
