#pragma once

#include <QString>

class QWebEngineProfile;

namespace AssetUrlScheme {

void registerSchemes();
void installHandlers(QWebEngineProfile *profile, const QString &assetsRoot);

QString hostToFolder(const QString &host);

} // namespace AssetUrlScheme
