# Release notes — OBAID Pricing 1.0.1

**Release date:** 2026-07-31  
**Platform:** Windows 10/11 x64 (self-contained)

## What’s new

- **In-app updates:** Settings → Application → Updates (check now, update policy).
- Updates download from GitHub Releases; your data under `%LocalAppData%\CostWise\` is kept.
- Database snapshot before each applied update.
- Reset clears Compare checkboxes (Nutrition Profiles / Formulations).

## Install / upgrade

1. Run **`ObaidPricing-win-Setup.exe`** from this release (or from `artifacts\velopack\` after a local build).
2. If you already have 1.0.0 data, just install over / install Velopack once — **do not** delete `%LocalAppData%\CostWise\`.
3. After install, confirm Settings → Application shows **Updates** and version **1.0.1**.

Later releases: open Updates → **Check for updates now** (no reinstall needed once you are on Velopack 1.0.1+).

## Known limitations

- First upgrade from a non-Velopack / Inno-only install requires running this Setup once.
- Setup may be unsigned (SmartScreen).
- SQLite DB is not encrypted at rest.
