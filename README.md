# OBAID Pricing

Windows WPF app for fish feed formulations, costing, and pricing.

**Documentation:** see the [docs/](docs/README.md) index (architecture, install, updates, user manual, release checklist).

## Requirements

- Windows 10/11 (x64)
- .NET 8 SDK (for building only; the Release publish is self-contained)

## Run (development)

```bash
dotnet run --project src/CostWise.App
```

## Publish (production 1.0.1)

```bash
dotnet publish src/CostWise.App/CostWise.App.csproj -p:PublishProfile=Win64Folder
```

Output: `artifacts/release/1.0.1/` (self-contained **folder** publish, win-x64 — used by Velopack).

## Installer and updates (Velopack — primary)

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-velopack.ps1
```

Produces packages under `artifacts/velopack/` for GitHub Releases. The app checks [`vulf-lab/obaid-price`](https://github.com/vulf-lab/obaid-price) and prompts before updating by default.

See **[docs/update-process.md](docs/update-process.md)**.

Legacy machine-wide Inno Setup: [installer/README.md](installer/README.md) (`build-installer.ps1`).

## Data location

All runtime data lives under `%LocalAppData%\CostWise\` (independent of the install folder):

| Path | Purpose |
|------|---------|
| `costwise.db` | SQLite database |
| `user-account.dat` | Local login account (DPAPI-protected) |
| `app-preferences.json` | UI preferences (including update policy) |
| `logs\` | Application logs |
| `backups\` | Pre-update database snapshots |

## Operator tools (optional)

Import formulations from Excel (requires an explicit database path):

```powershell
dotnet run --project tools/CostWise.Import -- "C:\path\to\formulations.xlsx" --db "%LOCALAPPDATA%\CostWise\costwise.db"
# or: --force-local
```

Expected columns: Category, Size, Specie, Feed Type, Code, Rev, Sub-Category, RM/Description, %
