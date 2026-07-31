# Build process

## Prerequisites

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- For Velopack packs: `dotnet tool install -g vpk` (also installed by `build-velopack.ps1`)
- For legacy Inno: Inno Setup 6 (optional; script can winget-install)

## Common commands

```powershell
# Restore / build solution
dotnet build CostWise.sln -c Release

# Unit tests
dotnet test CostWise.sln -c Release

# Run app (development — not a Velopack install)
dotnet run --project src/CostWise.App

# Folder publish (Release)
dotnet publish src/CostWise.App/CostWise.App.csproj -p:PublishProfile=Win64Folder
```

Publish output: `artifacts/release/1.0.0/` (`ObaidPricing.exe` + dependencies).

## Release pack (primary)

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-velopack.ps1
# or skip republish:
powershell -File installer\build-velopack.ps1 -SkipPublish
```

Output: `artifacts/velopack/` — see [installer-process.md](installer-process.md) and [update-process.md](update-process.md).

Smoke check:

```powershell
powershell -ExecutionPolicy Bypass -File installer\validate-velopack.ps1 -SkipBuild
```

## Version bump checklist

Keep these synchronized when cutting a release (currently `1.0.0`):

1. [`src/CostWise.App/CostWise.App.csproj`](../src/CostWise.App/CostWise.App.csproj) — `Version`, `AssemblyVersion`, `FileVersion`, `InformationalVersion`
2. [`Win64Folder.pubxml`](../src/CostWise.App/Properties/PublishProfiles/Win64Folder.pubxml) — `PublishDir` path segment
3. [`installer/build-velopack.ps1`](../installer/build-velopack.ps1) — `$version`
4. [`installer/validate-velopack.ps1`](../installer/validate-velopack.ps1) — `$version`
5. Legacy Inno (if used): `ObaidPricing.iss` (`MyAppVersion`, `PublishDir`), `build-installer.ps1`, `validate-release.ps1`
6. Docs / README version references as needed

There is no shared `Directory.Build.props` version yet.

## Configuration for CI / signing

Optional after pack:

```powershell
$env:OBAD_SIGN_CERT = "C:\secure\codesign.pfx"
$env:OBAD_SIGN_PASSWORD = "..."
```

`build-velopack.ps1` and `build-installer.ps1` invoke `signtool` when the cert path exists.

## Solution projects built

| Project | Output |
|---------|--------|
| CostWise.Core | Class library |
| CostWise.Infrastructure | Class library |
| CostWise.App | `ObaidPricing` WinExe |
| CostWise.*.Tests | Test assemblies |
| tools/* | Console tools (not required for app publish) |
