# Unused symbols baseline

Static analysis baseline for DeepSeek Desktop. **New PRs must not add unused public APIs**; private dead code is cleaned incrementally.

## C# (Roslyn / IDE0051 / IDE0052)

Enabled via `Directory.Build.props` (`AnalysisLevel=latest`, `Roslynator.Analyzers`). Warnings are report-only (`NoWarn` for IDE0051/IDE0052) until the backlog shrinks.

### Known removed in phase 1 (reference count was 0)

| Symbol | Location |
|--------|----------|
| `Chat2ApiConsoleHost` | removed (phase 1) |
| `Chat2ApiConsoleWindow` | removed (phase 1) |
| `AppNavigation.Chat2ApiAgentEmbedUrl` | renamed → `EmbeddedApiManagementUrl` |
| `DesktopWebHost.NavigateAgentAsync` | `Services/DesktopWebHost.cs` |
| `IDdWebPages.AfterShowChatAsync` | `Services/IDdWebPages.cs` |
| `IHarnessRunner` | `DeepSeek.Core/Services/Harness/IHarnessRunner.cs` |
| `AppNavigation.AgentUiBuild` | alias removed |
| `IDdWebPages.IsAgentHostPage` | interface member removed |

### Regenerate build report

```powershell
cd deepseek_desktop-source
.\scripts\analyze-build.ps1
```

## JavaScript

Scope: `Assets/agent/*.js`, `Assets/dsd-api-ui/*.js` (not Vite bundles under `Assets/dsd-api/assets/index-*.js`).

Optional local check (requires Node):

```powershell
cd deepseek_desktop-source
npm install
npm run lint:agent
```

## Policy

1. Do not hand-edit `Assets/dsd-api/assets/index-*.js`.
2. Bump `AppNavigation.EmbeddedUiBuild` when changing shipped HTML/CSS/JS.
3. Run `dotnet test` and `build.ps1` before merge.
