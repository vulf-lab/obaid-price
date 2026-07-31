# Installer process

## Primary: Velopack

OBAID Pricing is installed and updated with **Velopack** (per-user under `%LocalAppData%\ObaidPricing\`).

### Build

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-velopack.ps1
```

| Output (under `artifacts/velopack/`) | Role |
|--------------------------------------|------|
| `ObaidPricing-win-Setup.exe` | First-time installer |
| `*-full.nupkg` (+ deltas when applicable) | Update packages |
| `releases.win.json` | Feed consumed by clients |

Pack id: `ObaidPricing`. Main exe: `ObaidPricing.exe`. Icon: `src/CostWise.App/Assets/obaid-pricing-logo.ico`.

### Validate pack artifacts

```powershell
powershell -ExecutionPolicy Bypass -File installer\validate-velopack.ps1
```

Checks that nupkg, releases JSON, and Setup exist. Full UI install remains a manual smoke test.

### First install for users

1. Distribute `ObaidPricing-win-Setup.exe` (or GitHub Release assets).
2. User runs Setup (per-user; no Program Files admin required for typical Velopack layout).
3. First launch: password setup / login; DB created under `%LocalAppData%\CostWise\`.

Details: [update-process.md](update-process.md), [deployment-guide.md](deployment-guide.md).

---

## Legacy: Inno Setup 6

Machine-wide installer under `%ProgramFiles%\OBAID\OBAID Pricing`. **Does not** receive GitHub auto-updates. Kept for offline / elevated scenarios.

| Setting | Value |
|---------|--------|
| Script | [`installer/ObaidPricing.iss`](../installer/ObaidPricing.iss) |
| Build | [`installer/build-installer.ps1`](../installer/build-installer.ps1) |
| AppId | `{A7E3C91D-4B2F-4E8A-9C1D-6F0E5B8A2D71}` (stable for Inno upgrades) |
| Privileges | `admin` |
| Output | `artifacts/installer/ObaidPricing-Setup-1.0.0.exe` |

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
```

Silent install example (validation):

```text
ObaidPricing-Setup-1.0.0.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR="..." /TASKS=
```

Clean-install simulation: [`installer/validate-release.ps1`](../installer/validate-release.ps1) (isolates LocalAppData, silent install/uninstall, writes `artifacts/release/RELEASE_VALIDATION_REPORT.md`).

Full Inno notes: [installer/README.md](../installer/README.md).

### Migrating Inno → Velopack

Uninstall Inno copy (optional; **keeps** `%LocalAppData%\CostWise\`), then install Velopack Setup.
