# Changelog

All notable changes to OBAID Pricing are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project uses [Semantic Versioning](https://semver.org/).

## [1.0.4] - 2026-10-02

### Fixed

- Last Price follows feed type and size, so a new formula code for the same product still shows last month's price.

## [1.0.3] - 2026-10-02

### Added

- Saved briefs tab to open, download, or delete past Victory price lists.
- Use a saved brief as the Last Price comparison.

### Fixed

- Last Price still matches after a formula or price book is replaced, using formula code and book side.

## [1.0.2] - 2026-07-31

### Fixed

- Settings version display shows `1.0.2` only (no git commit hash suffix).

## [1.0.1] - 2026-07-31

### Added

- In-app updates via Velopack + GitHub Releases (Settings → Application → Updates).
- Update policies: prompt (default), silent download/apply on restart, or off.
- Pre-update database snapshots under `%LocalAppData%\CostWise\backups\`.

### Fixed

- Reset on Nutrition Profiles / Formulations also clears Compare checkboxes.

## [1.0.0] - 2026-07-30

### Added

- Windows desktop app for formulations, nutrition profiles, comparison, production matrix, raw ingredients, costing/pricing, and price lists (commercial + Victory).
- Local login: first-run password setup, optional PIN, remember-me with required secret, lockout, DPAPI-protected account store.
- Profile page for password/PIN/sign-out.
- Settings grouped by Product / Pricing / Nutrition / Application (masters, cost options, currencies, display decimals, branding logos).
- SQLite database under `%LocalAppData%\CostWise\` with EF Core migrations on startup.
- Self-contained win-x64 publish; **Velopack** installer and GitHub Releases auto-update (prompt by default).
- Pre-update database snapshots and restore prompt if migration fails after update.
- Legacy Inno Setup machine-wide installer (optional; no GitHub auto-update).
- Optional CLI tools for Excel import, sheet apply, and price updates (`--db` / `--force-local` guard).
- Application file logging under `%LocalAppData%\CostWise\logs\`.

### Security

- No default password in source; fail-closed if account file is damaged.
- Remember-me never passwordless when PIN/password required.

### Known limitations

- SQLite not encrypted at rest (login gates UI only).
- Installer may be unsigned (SmartScreen).
- No in-app full backup wizard (folder copy / automatic DB snapshots only).

[1.0.0]: https://github.com/vulf-lab/obaid-price/releases/tag/v1.0.0
