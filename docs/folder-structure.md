# Folder structure

Repository root: CostWise / OBAID Pricing.

```
costwise/
├── CostWise.sln
├── README.md
├── docs/                      # This documentation suite
├── src/
│   ├── CostWise.App/          # WPF executable (ObaidPricing)
│   │   ├── Assets/
│   │   ├── Controls/
│   │   ├── Converters/
│   │   ├── Properties/PublishProfiles/
│   │   ├── Services/          # Auth, prefs, log, update, import helpers, export
│   │   ├── Themes/
│   │   ├── ViewModels/
│   │   ├── Views/
│   │   ├── App.xaml(.cs)
│   │   ├── Program.cs         # Velopack + WPF Main
│   │   └── MainWindow.xaml(.cs)
│   ├── CostWise.Core/         # Entities, enums, domain services
│   │   ├── Entities/
│   │   ├── Enums/
│   │   └── Services/
│   └── CostWise.Infrastructure/
│       ├── Data/              # DbContext, DbSeeder, Migrations/
│       ├── Services/          # Normalizers, SystemId, etc.
│       ├── DependencyInjection.cs
│       └── ToolDatabaseArgs.cs
├── tests/
│   ├── CostWise.App.Tests/
│   └── CostWise.Core.Tests/
├── tools/
│   ├── CostWise.Import/
│   ├── CostWise.ApplySheet/
│   ├── CostWise.UpdatePrices/
│   └── CostWise.WriteImportSamples/
├── samples/
│   └── import/                # Excel templates + README
├── installer/
│   ├── build-velopack.ps1     # Primary pack (Velopack)
│   ├── validate-velopack.ps1
│   ├── build-installer.ps1    # Legacy Inno
│   ├── validate-release.ps1
│   ├── ObaidPricing.iss
│   └── README.md
└── artifacts/                 # Generated (gitignored)
    ├── release/1.0.0/         # Folder publish output
    ├── velopack/              # Setup + nupkg + releases feed
    └── installer/             # Legacy Inno Setup.exe
```

## Notable App subfolders

| Path | Contents |
|------|----------|
| `Services/Update/` | `UpdateService`, `UpdatePolicy`, `DatabaseBackupService` |
| `Services/Import/` | In-app Excel import helpers |
| `Views/` | Page UserControls + `LoginWindow`, `UpdateAvailableWindow` |

## Generated / local-only

Do not commit secrets or machine-specific DBs. `artifacts/` and `%LocalAppData%\CostWise\` are runtime/build outputs.
