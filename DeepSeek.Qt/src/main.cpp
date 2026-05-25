#include "AssetUrlScheme.h"
#include "MainWindow.h"

#include <QApplication>
#include <QCoreApplication>
#include <QDir>
#include <QScreen>

int main(int argc, char *argv[])
{
    AssetUrlScheme::registerSchemes();

    QCoreApplication::setAttribute(Qt::AA_ShareOpenGLContexts);
    QApplication app(argc, argv);

    QString publishDir = QCoreApplication::applicationDirPath();
    for (int i = 1; i < argc; ++i) {
        const QString arg = QString::fromLocal8Bit(argv[i]);
        if (arg.startsWith(QStringLiteral("--publish-dir="))) {
            publishDir = arg.mid(14);
            break;
        }
    }

    MainWindow window(publishDir);
    window.move(QApplication::primaryScreen()->availableGeometry().center() - window.rect().center());
    window.show();

    return app.exec();
}
