# DeepSeek Desktop (DD) naming

**DD** = **DeepSeek Desktop** (product shell: WPF, Qt hybrid, Bridge). Not the DeepSeek company cloud API.

## C# convention (single style)

| Use | Example |
|-----|---------|
| **`Dd` prefix** — types, members, namespaces under desktop shell | `DdDesktopIpc`, `DdBridgeWebHost`, `IDdWebPages`, `DeepSeekBrowser.Services.Dd` |
| **`DD` / `DEEPSEEK_DESKTOP_*`** — only if we add env vars or native macros later | reserved |
| **Pipe / protocol string literals** — lowercase kebab | `dd-desktop-bridge`, `ddReady`, `ddSurface` |

Do **not** mix `QtDesktop*` / `IDesktopWebPages` for new DD shell code; **Qt** stays only where it means the **Qt 6 toolkit** (`DeepSeek.Qt`, `QWebEngine`, `Test-DdQtToolchain.ps1`).

## Rename map (legacy → current)

| Legacy | Current |
|--------|---------|
| `IDesktopWebPages` | `IDdWebPages` |
| `IWebPageMessenger` | `IDdPageMessenger` |
| `Services/Qt/` | `Services/Dd/` |
| `QtDesktopIpc` | `DdDesktopIpc` |
| `QtBridgeDesktopWebHost` | `DdBridgeWebHost` |
| `QtPipePageMessenger` | `DdPipePageMessenger` |
| `deepseek-desktop-bridge` | `dd-desktop-bridge` |
| `qtReady` / `qtSurface` | `ddReady` / `ddSurface` |
| `DeepSeek.QtBridge` (project) | `DeepSeek.DdBridge` |
| `DeepSeekBrowser.QtBridge` | `DeepSeekBrowser.DdBridge` |
| `qt-webview-shim.js` | `dd-webview-shim.js` |
| `verify-qt-ipc.ps1` | `verify-dd-ipc.ps1` |
| `Test-QtToolchain.ps1` | `Test-DdQtToolchain.ps1` |
| `QT_DESKTOP.md` | `DD_DESKTOP.md` |

## Intentional exceptions (unchanged publish / UX)

| Item | Reason |
|------|--------|
| `DeepSeek.exe` | Runtime launcher |
| `DeepSeek.App.exe` | WPF main binary |
| `DeepSeek.Bridge.exe` | Bridge assembly name (`AssemblyName` in csproj) |
| `DeepSeek.Qt.exe` | Qt **toolkit** shell binary |
| `DeepSeekBrowser` | Root WPF project / namespace for legacy host |
| `fanstars2318/deepseek-desktop` | GitHub repo slug |
| `DesktopWebHost`, `DesktopAgentHost` | WPF-path implementations; implement `IDdWebPages` |

## Docs

- Hybrid shell: [DD_DESKTOP.md](./DD_DESKTOP.md)
