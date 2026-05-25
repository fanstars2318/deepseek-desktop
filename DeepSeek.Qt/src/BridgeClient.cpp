#include "BridgeClient.h"

#include <QJsonDocument>
#include <QJsonObject>
#include <QTimer>

BridgeClient::BridgeClient(QObject *parent)
    : QObject(parent)
{
    connect(&m_socket, &QLocalSocket::readyRead, this, &BridgeClient::onReadyRead);
    connect(&m_socket, &QLocalSocket::disconnected, this, &BridgeClient::onDisconnected);
}

bool BridgeClient::connectToBridge(int timeoutMs)
{
    m_socket.connectToServer(QString::fromUtf8(PipeName));
    if (!m_socket.waitForConnected(timeoutMs))
        return false;
    return true;
}

void BridgeClient::sendEnvelope(const QString &channel, const QJsonObject &payload)
{
    if (m_socket.state() != QLocalSocket::ConnectedState)
        return;

    QJsonObject envelope;
    envelope.insert(QStringLiteral("channel"), channel);
    envelope.insert(QStringLiteral("payload"), payload);
    const QByteArray line = QJsonDocument(envelope).toJson(QJsonDocument::Compact) + '\n';
    m_socket.write(line);
    m_socket.flush();
}

void BridgeClient::sendControl(const QJsonObject &payload)
{
    sendEnvelope(QStringLiteral("control"), payload);
}

void BridgeClient::onReadyRead()
{
    m_buffer.append(m_socket.readAll());
    int idx = -1;
    while ((idx = m_buffer.indexOf('\n')) >= 0) {
        const QByteArray line = m_buffer.left(idx);
        m_buffer.remove(0, idx + 1);
        processLine(line);
    }
}

void BridgeClient::onDisconnected()
{
    emit disconnected();
}

void BridgeClient::processLine(const QByteArray &line)
{
    if (line.trimmed().isEmpty())
        return;

    QJsonParseError err;
    const QJsonDocument doc = QJsonDocument::fromJson(line, &err);
    if (err.error != QJsonParseError::NoError || !doc.isObject())
        return;

    const QJsonObject root = doc.object();
    const QString channel = root.value(QStringLiteral("channel")).toString();
    const QJsonObject payload = root.value(QStringLiteral("payload")).toObject();
    if (channel.isEmpty())
        return;

    emit envelopeReceived(channel, payload);
}
