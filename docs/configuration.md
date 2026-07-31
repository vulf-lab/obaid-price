# Configuration

## User data directory

All runtime configuration and data (except the installed binaries) live under:

`%LocalAppData%\CostWise\`

| Path | Purpose |
|------|---------|
| `costwise.db` | SQLite database |
| `user-account.dat` | Login account (DPAPI); may migrate from legacy `user-account.json` |
| `app-preferences.json` | UI + update preferences |
| `logs\costwise-yyyyMMdd.log` | Application log (`AppLog`) |
| `backups\pre-update-*.db` | Automatic DB snapshots before apply-update |
| `branding\` | PDF logos (`victory-left`, `victory-right`, `commercial`) |
| `grid-column-layouts.json` | Column layout / visibility |
| `grid-column-widths.json` | Column widths |
| `column-filters.json` | Grid filters |
| `compare-selection.json` | Comparison selection |
| `commercial-price-list-prefs.json` | Commercial PDF prefs |
| `victory-price-list-prefs.json` | Victory PDF prefs |

Velopack binaries: `%LocalAppData%\ObaidPricing\` (do not store user data there).

## App preferences (`app-preferences.json`)

[`AppPreferences`](../src/CostWise.App/Services/AppPreferences.cs):

| Field | Default | Meaning |
|-------|---------|---------|
| `CostDecimalPlaces` | 2 | Display decimals for costs (0–4) |
| `KesDecimalPlaces` / `UsdDecimalPlaces` | 2 | Display decimals for prices |
| `ExchangeRateKesPerUsd` | 130 | Fallback rate (prefer Currency table when present) |
| `NavOrder` | `[]` | Sidebar page key order |
| `UpdatePolicy` | `Prompt` | `Prompt` \| `SilentDownloadApplyOnRestart` \| `Off` |
| `LastUpdateCheckUtc` | null | Throttle for background checks |
| `SkippedUpdateVersion` | null | Version user chose to skip |

## Auth

[`AuthAccountStore`](../src/CostWise.App/Services/AuthAccountStore.cs):

- First run: password setup (email default in product; password min length 8)
- Optional PIN (4–8 digits); remember-me still requires PIN or password
- Lockout after repeated failures

## Environment variables

| Variable | Effect |
|----------|--------|
| `COSTWISE_RUN_ORPHAN_CLEANUP=1` | One-shot orphan formulation cleanup during DB init |
| `COSTWISE_UPDATE_URL` | Override Velopack update source (folder or HTTP feed) for testing |
| `OBAD_SIGN_CERT` | Path to `.pfx` for signing Setup (build scripts) |
| `OBAD_SIGN_PASSWORD` | Optional PFX password |

## Publish profile

[`Win64Folder.pubxml`](../src/CostWise.App/Properties/PublishProfiles/Win64Folder.pubxml):

- `net8.0-windows`, `win-x64`, **self-contained**
- **Folder** publish (`PublishSingleFile=false`) for Velopack
- Output: `artifacts/release/1.0.0/`

## Update feed (code)

Default GitHub repo: `https://github.com/vulf-lab/obaid-price` (`UpdateService.GitHubRepoUrl`). Prereleases are ignored.
