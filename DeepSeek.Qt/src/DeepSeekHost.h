#pragma once

#include <QObject>

class BridgeClient;

class DeepSeekHost : public QObject
{
    Q_OBJECT

public:
    explicit DeepSeekHost(BridgeClient *bridge, const QString &channel, QObject *parent = nullptr);

public slots:
    void receiveMessage(const QString &json);

signals:
    void sendMessage(const QString &json);

private:
    BridgeClient *m_bridge = nullptr;
    QString m_channel;
};
