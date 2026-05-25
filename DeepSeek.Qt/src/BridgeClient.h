#pragma once

#include <QObject>
#include <QLocalSocket>

class BridgeClient : public QObject
{
    Q_OBJECT

public:
    static constexpr const char *PipeName = "dd-desktop-bridge";

    explicit BridgeClient(QObject *parent = nullptr);

    bool connectToBridge(int timeoutMs = 15000);
    void sendEnvelope(const QString &channel, const QJsonObject &payload);
    void sendControl(const QJsonObject &payload);

signals:
    void envelopeReceived(const QString &channel, const QJsonObject &payload);
    void disconnected();

private slots:
    void onReadyRead();
    void onDisconnected();

private:
    void processLine(const QByteArray &line);

    QLocalSocket m_socket;
    QByteArray m_buffer;
};
