#include "AssetUrlScheme.h"

#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QMimeDatabase>
#include <QMimeType>
#include <QUrl>
#include <QWebEngineProfile>
#include <QWebEngineUrlRequestJob>
#include <QWebEngineUrlScheme>
#include <QWebEngineUrlSchemeHandler>

namespace {

class FolderSchemeHandler : public QWebEngineUrlSchemeHandler
{
public:
    explicit FolderSchemeHandler(QString rootDir, QObject *parent = nullptr)
        : QWebEngineUrlSchemeHandler(parent)
        , m_rootDir(std::move(rootDir))
    {
    }

    void requestStarted(QWebEngineUrlRequestJob *job) override
    {
        if (m_rootDir.isEmpty()) {
            job->fail(QWebEngineUrlRequestJob::UrlNotFound);
            return;
        }

        const QUrl url = job->requestUrl();
        QString path = url.path();
        if (path.startsWith(QLatin1Char('/')))
            path = path.mid(1);

        const QString filePath = QDir(m_rootDir).filePath(path);
        if (!QFile::exists(filePath)) {
            job->fail(QWebEngineUrlRequestJob::UrlNotFound);
            return;
        }

        QFile *file = new QFile(filePath);
        if (!file->open(QIODevice::ReadOnly)) {
            delete file;
            job->fail(QWebEngineUrlRequestJob::UrlNotFound);
            return;
        }

        const QMimeType mime = QMimeDatabase().mimeTypeForFile(filePath);
        job->reply(mime.name().toUtf8(), file);
    }

private:
    QString m_rootDir;
};

void registerOneScheme(const char *name)
{
    QWebEngineUrlScheme scheme(name);
    scheme.setSyntax(QWebEngineUrlScheme::Syntax::Host);
    scheme.setDefaultPort(443);
    scheme.setFlags(QWebEngineUrlScheme::SecureScheme | QWebEngineUrlScheme::LocalScheme |
                   QWebEngineUrlScheme::LocalAccessAllowed | QWebEngineUrlScheme::CorsEnabled);
    QWebEngineUrlScheme::registerScheme(scheme);
}

} // namespace

QString AssetUrlScheme::hostToFolder(const QString &host)
{
    if (host == QLatin1String("ds-agent.local"))
        return QStringLiteral("agent");
    if (host == QLatin1String("ds-inject.local"))
        return QStringLiteral("inject");
    if (host == QLatin1String("ds-chat2api.local"))
        return QStringLiteral("chat2api");
    return {};
}

void AssetUrlScheme::registerSchemes()
{
    registerOneScheme("ds-agent");
    registerOneScheme("ds-inject");
    registerOneScheme("ds-chat2api");
}

void AssetUrlScheme::installHandlers(QWebEngineProfile *profile, const QString &assetsRoot)
{
    const auto install = [&](const char *scheme, const QString &folder) {
        const QString dir = QDir(assetsRoot).filePath(folder);
        profile->installUrlSchemeHandler(scheme, new FolderSchemeHandler(dir, profile));
    };
    install("ds-agent", QStringLiteral("agent"));
    install("ds-inject", QStringLiteral("inject"));
    install("ds-chat2api", QStringLiteral("chat2api"));
}
