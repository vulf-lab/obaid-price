# Developer setup

## Requirements

- Windows 10/11 x64
- .NET 8 SDK
- Visual Studio 2022 / Rider / VS Code + C# extension (optional)
- Git

## Clone and run

```powershell
git clone https://github.com/vulf-lab/obaid-price.git
cd obaid-price   # or costwise
dotnet restore CostWise.sln
dotnet run --project src/CostWise.App
```

First run creates `%LocalAppData%\CostWise\` and prompts for password setup.

**Note:** `dotnet run` is **not** a Velopack install. Auto-update checks will no-op until you install `ObaidPricing-win-Setup.exe`.

## Tests

```powershell
dotnet test CostWise.sln -c Release
```

- `tests/CostWise.Core.Tests` — costing, formulation rules, import validators  
- `tests/CostWise.App.Tests` — auth hashing/lockout/PIN, update policy parsing  

## Solution map

| Project | Purpose |
|---------|---------|
| `src/CostWise.App` | WPF UI (`ObaidPricing`) |
| `src/CostWise.Core` | Entities, enums, pure domain logic |
| `src/CostWise.Infrastructure` | EF Core SQLite, migrations |
| `tools/*` | Optional CLI (see below) |

See [architecture.md](architecture.md) and [folder-structure.md](folder-structure.md).

## EF migrations

```powershell
dotnet ef migrations add <Name> `
  --project src/CostWise.Infrastructure `
  --startup-project src/CostWise.App
```

Migrations apply automatically on app start (`MigrateAsync`).

## CLI tools

All tools require `--db <path>` or `--force-local` (refuse silent use of production LocalAppData).

```powershell
# Import formulations
dotnet run --project tools/CostWise.Import -- "samples\import\....xlsx" --db "$env:LOCALAPPDATA\CostWise\costwise.db"

# Apply sheet actions (dry-run default; add --apply)
dotnet run --project tools/CostWise.ApplySheet -- "sheet.xlsx" --db "..."

# Update RM prices from embedded sheet
dotnet run --project tools/CostWise.UpdatePrices -- --db "..." [--apply]

# Regenerate import samples
dotnet run --project tools/CostWise.WriteImportSamples
```

Sample Excel templates: `samples/import/` (see that folder’s README).

## Local update testing

1. Pack: `installer\build-velopack.ps1`
2. Install Setup once
3. Bump version, pack again into a folder
4. `$env:COSTWISE_UPDATE_URL = "<that folder>"` then **Check for updates now**

See [update-process.md](update-process.md).

## Useful env vars

| Variable | Use |
|----------|-----|
| `COSTWISE_UPDATE_URL` | Local/HTTP update feed override |
| `COSTWISE_RUN_ORPHAN_CLEANUP=1` | One-shot orphan formulation cleanup |
| `OBAD_SIGN_CERT` / `OBAD_SIGN_PASSWORD` | Code signing during pack scripts |
