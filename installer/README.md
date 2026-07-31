# Inno Setup installer (legacy)

> **Primary distribution is now Velopack.** Use [`build-velopack.ps1`](build-velopack.ps1) and see [`../docs/update-process.md`](../docs/update-process.md).  
> This Inno Setup path remains for historical / offline machine-wide installs under Program Files. It does **not** participate in GitHub auto-update.

Professional installer for **OBAID Pricing** 1.0.0 (machine-wide).

## What you get

| Feature | Behavior |
|---------|----------|
| Product / company / version | OBAID Pricing · OBAID · 1.0.0 |
| Application icon | `src/CostWise.App/Assets/obaid-pricing-logo.ico` |
| Install directory | Choosable (default `%ProgramFiles%\OBAID\OBAID Pricing`) |
| Start Menu | App + Uninstall |
| Desktop shortcut | Optional (off by default) |
| Uninstall | Windows Apps & Features / Start Menu Uninstall |
| Runtime dependencies | None — packages the **self-contained** publish (no .NET Desktop Runtime prerequisite) |
| Code signing | Signed-ready; enabled when `OBAD_SIGN_CERT` is set |
| Auto-update | **No** — migrate to Velopack Setup for updates |

## Prerequisites

- Windows 10/11 x64
- .NET 8 SDK (to publish)
- [Inno Setup 6](https://jrsoftware.org/isdl.php) — or let the build script install it via `winget`

## Build commands

From the repo root:

```powershell
# Publish + compile installer (installs Inno Setup via winget if missing)
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
```

If you already published:

```powershell
dotnet publish src/CostWise.App/CostWise.App.csproj -p:PublishProfile=Win64Folder
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1 -SkipPublish
```

**Output:** `artifacts\installer\ObaidPricing-Setup-1.0.0.exe`

## Optional code signing

```powershell
$env:OBAD_SIGN_CERT = "C:\secure\codesign.pfx"
$env:OBAD_SIGN_PASSWORD = "your-pfx-password"
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
```

## Uninstall / data

Uninstall removes files under the chosen install directory only.  
Database and preferences under `%LocalAppData%\CostWise\` are kept so reinstall preserves user data.

## Migrating to Velopack

1. Optional: uninstall the Inno copy (keeps `%LocalAppData%\CostWise\`).
2. Install the Velopack Setup from GitHub Releases.
3. Auto-updates then work via Settings → Application → Updates.
