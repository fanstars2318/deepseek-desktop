#include "DeepSeekHost.h"

#include "BridgeClient.h"

#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonParseError>

DeepSeekHost::DeepSeekHost(BridgeClient *bridge, const QString &channel, QObject *parent)
    : QObject(parent)
    , m_bridge(bridge)
    , m_channel(channel)
{
}

void DeepSeekHost::receiveMessage(const QString &json)
{
    if (!m_bridge)
        return;

    QJsonParseError err;
    const QJsonDocument doc = QJsonDocument::fromJson(json.toUtf8(), &err);
    QJsonObject payload;
    if (err.error == QJsonParseError::NoError && doc.isObject())
        payload = doc.object();
    else
        payload.insert(QStringLiteral("raw"), json);

    m_bridge->sendEnvelope(m_channel, payload);
}
