# Update process (Velopack + GitHub)

OBAID Pricing ships and updates with **[Velopack](https://velopack.io/)**. The update feed is **GitHub Releases** on [`vulf-lab/obaid-price`](https://github.com/vulf-lab/obaid-price).

## Install layout

| Path | Purpose |
|------|---------|
| `%LocalAppData%\ObaidPricing\` | Velopack install (`current\`, `Update.exe`, packages) |
| `%LocalAppData%\CostWise\` | User data (DB, login, prefs, logs, backups) — **never** stored under Velopack `current\` |

Binary updates replace only the app under `ObaidPricing\`. User data is preserved. Schema upgrades run on next start via EF `MigrateAsync`.

## User experience

Default policy: **Prompt** (ask before download/apply).

| Policy | Behavior |
|--------|----------|
| Ask before downloading (default) | Dialog with version + notes; Update now / Later / Skip this version |
| Download quietly; apply on restart | Downloads in the background; optional restart now; otherwise applies on next launch |
| Do not check for updates | No network checks |

Settings → **Application** → **Updates**: change policy, see version, **Check for updates now**.

Checks are throttled to about once per day unless forced from Settings.

## Database safety

Before applying an update, the app copies:

`%LocalAppData%\CostWise\backups\pre-update-{version}-{timestamp}.db`

(keeps the latest 5). If `MigrateAsync` fails after an update, the user is offered restore of the latest backup, then exit. App binary rollback = install a prior GitHub/Velopack release.

## Build a release

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-velopack.ps1
```

Optional notes:

```powershell
powershell -File installer\build-velopack.ps1 -ReleaseNotes "## Fixes`n- ..."
```

Output: `artifacts\velopack\` (Setup.exe, `.nupkg`, `releases.*.json`).

### Publish to GitHub

1. Create a release tag matching the pack version (e.g. `v1.0.1`).
2. Upload **all** files from `artifacts\velopack\` (do not rename assets).
3. Publish the release (non-prerelease for production; the app ignores prereleases).

Clients use:

```csharp
new GithubSource("https://github.com/vulf-lab/obaid-price", accessToken: null, prerelease: false);
```

### Local testing without GitHub

```powershell
$env:COSTWISE_UPDATE_URL = "C:\path\to\artifacts\velopack"
```

Then run a Velopack-installed build (not `dotnet run`) and use **Check for updates now**.

Smoke pack check:

```powershell
powershell -ExecutionPolicy Bypass -File installer\validate-velopack.ps1
```

## Version bump checklist

Keep these in sync when cutting a release:

1. `src/CostWise.App/CostWise.App.csproj` — `Version` / `InformationalVersion` / file versions  
2. `src/CostWise.App/Properties/PublishProfiles/Win64Folder.pubxml` — `PublishDir`  
3. `installer/build-velopack.ps1` — `$version`  
4. `installer/validate-velopack.ps1` — `$version`  
5. Legacy Inno (if still used): `ObaidPricing.iss`, `build-installer.ps1`, `validate-release.ps1`

## Development builds

`dotnet run` / Visual Studio debug is **not** a Velopack install. Update checks no-op with a clear message when forced from Settings.

## Legacy Inno Setup

Machine-wide Inno installs under Program Files do **not** receive GitHub auto-updates. Migrate by installing the Velopack Setup (LocalAppData CostWise data is kept). See [installer/README.md](../installer/README.md).
