# Architecture

OBAID Pricing is a **.NET 8 WPF** desktop app (assembly / exe name `ObaidPricing`) for fish-feed formulations, costing, and pricing. The solution nickname in source is **CostWise**.

## Layers

```mermaid
flowchart TB
  subgraph ui [CostWise.App]
    Program[Program.Main Velopack]
    App[App.xaml.cs]
    Views[Views / Windows]
    VMs[ViewModels]
    Svcs[App services]
  end
  subgraph core [CostWise.Core]
    Ent[Entities / Enums]
    Rules[CostingCalculator / FormulationRules]
  end
  subgraph infra [CostWise.Infrastructure]
    DI[DependencyInjection]
    Db[CostWiseDbContext]
    Mig[EF Migrations]
  end
  Program --> App
  App --> VMs
  Views --> VMs
  VMs --> Svcs
  VMs --> Db
  Svcs --> Ent
  DI --> Db
  Db --> Mig
  Rules --> Ent
```

| Project | Role |
|---------|------|
| **CostWise.App** | WPF UI, MVVM, auth, updates, PDF/Excel export, DI host |
| **CostWise.Core** | Domain entities, enums, pure costing/import rules |
| **CostWise.Infrastructure** | EF Core SQLite, migrations, seeders, normalizers, CLI DB args |
| **tests/** | xUnit (Core costing/import; App auth/update prefs) |
| **tools/** | Optional CLI importers (explicit `--db` or `--force-local`) |

## Startup sequence

```mermaid
sequenceDiagram
  participant P as Program.Main
  participant V as VelopackApp
  participant A as App.OnStartup
  participant DB as InitializeDatabaseAsync
  participant L as LoginWindow
  participant M as MainWindow
  participant U as UpdateService
  P->>V: Build().SetAutoApplyOnStartup(false).Run()
  P->>A: InitializeComponent + Run
  A->>DB: MigrateAsync + seed + normalizers
  alt migrate fails and backup exists
    A-->>A: Offer restore from backups\
  end
  A->>L: AuthenticateInteractive
  L-->>A: Authenticated
  A->>M: Show
  A->>U: Background check (throttled)
```

Key files:

- [`src/CostWise.App/Program.cs`](../src/CostWise.App/Program.cs) — Velopack hooks before WPF
- [`src/CostWise.App/App.xaml.cs`](../src/CostWise.App/App.xaml.cs) — host, DI, DB init, login, update schedule
- [`src/CostWise.Infrastructure/DependencyInjection.cs`](../src/CostWise.Infrastructure/DependencyInjection.cs) — SQLite path + `MigrateAsync`

## UI pattern

- **MVVM** with CommunityToolkit.Mvvm (`ObservableObject`, `[RelayCommand]`)
- **Navigation:** `INavigationService` / `MainViewModel` sidebar keys (`formulations`, `nutrition`, `compare`, …)
- **ViewLocator** maps ViewModel type → View `UserControl`
- **Auth gate:** `LoginWindow` before main shell; `ShutdownMode.OnExplicitShutdown` so closing login does not kill the process prematurely

## Data and install separation

| Concern | Location |
|---------|----------|
| App binaries (Velopack) | `%LocalAppData%\ObaidPricing\` |
| User DB / prefs / auth / logs | `%LocalAppData%\CostWise\` |

Updates replace binaries only; schema evolves via EF migrations on next launch. See [update-process.md](update-process.md) and [database-schema.md](database-schema.md).

## Cross-cutting services (App)

| Service | Purpose |
|---------|---------|
| `AppPreferences` | Decimals, nav order, update policy (JSON) |
| `AuthAccountStore` / `AuthSession` | Local login (DPAPI) |
| `UpdateService` | Velopack + GitHub Releases |
| `DatabaseBackupService` | Pre-update SQLite snapshots |
| `AppLog` | File logs under `CostWise\logs` |
| `ProductionExportService` | Matrix / PDF / Excel exports |

## Security notes (product)

- Login gates the UI; **SQLite is not encrypted at rest** (file copy bypasses login).
- Password/PIN stored as salted hashes in DPAPI-protected `user-account.dat`.
- Lockout: 5 failed attempts → short cooldown.
