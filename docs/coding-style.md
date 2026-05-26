# Coding style (DeepSeek Desktop)

## Naming

| Area | Convention |
|------|------------|
| C# public API | ``Dsd` prefix for DSD API stack types |
| Documentation / user-facing | `DSD API` |
| Asset folders | `dsd-api`, `dsd-api-ui` (lowercase paths) |
| Desktop host interfaces | `IDesktop*` (e.g. `IDesktopWebHost`, `IDesktopWebSurfaces`) |
| Legacy | `IDdWebPages` — obsolete alias of `IDesktopWebHost` |

## URLs

- DeepSeek chat entry: `AppNavigation.DeepSeekUrl` only.
- Agent shell: `AppNavigation.AgentPageUrl` with `?build={EmbeddedUiBuild}`.
- DSD API embed inside Agent: built in `Assets/agent/agent-app.js` (`embedUrl("dsd-api/...")`), not in C# constants.

## API accounts vs normal chat

- **Normal mode** (`chat.deepseek.com`): may use `AppConfig.WebUserToken` after web login.
- **Agent API management**: accounts in `provider-accounts.json` only; do not auto-sync web login into the account list.

## UI assets

- Bump `AppNavigation.EmbeddedUiBuild` when changing shipped HTML/CSS/JS under `Assets/agent` or `Assets/dsd-api`.
- Do not hand-edit Vite output under `Assets/dsd-api/assets/index-*.js`.

## Analyzers

See [unused-symbols-baseline.md](unused-symbols-baseline.md).
