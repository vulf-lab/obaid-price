<#
.SYNOPSIS
  Publishes OBAID Pricing (Release, self-contained) and builds the Windows installer with Inno Setup 6.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1

.EXAMPLE
  powershell -File installer\build-installer.ps1 -SkipPublish
#>
[CmdletBinding()]
param(
    [switch]$SkipPublish,
    [switch]$SkipInnoInstall
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

$version = "1.0.1"
$publishDir = Join-Path $root "artifacts\release\$version"
$iss = Join-Path $root "installer\ObaidPricing.iss"
$outDir = Join-Path $root "artifacts\installer"
$icon = Join-Path $root "src\CostWise.App\Assets\obaid-pricing-logo.ico"

function Find-ISCC {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

Write-Host "==> OBAID Pricing installer build ($version)" -ForegroundColor Cyan

if (-not $SkipPublish) {
    Write-Host "==> Publishing self-contained Release (win-x64)..." -ForegroundColor Cyan
    dotnet publish (Join-Path $root "src\CostWise.App\CostWise.App.csproj") `
        -p:PublishProfile=Win64Folder
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }
}

$exe = Join-Path $publishDir "ObaidPricing.exe"
if (-not (Test-Path $exe)) {
    throw "Published exe not found: $exe. Run without -SkipPublish first."
}
if (-not (Test-Path $icon)) {
    throw "Setup icon not found: $icon"
}

Write-Host "==> Publish OK: $exe" -ForegroundColor Green

$iscc = Find-ISCC
if (-not $iscc -and -not $SkipInnoInstall) {
    Write-Host "==> Inno Setup 6 not found - attempting winget install..." -ForegroundColor Yellow
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if ($winget) {
        winget install --id JRSoftware.InnoSetup -e --accept-package-agreements --accept-source-agreements
        $iscc = Find-ISCC
    }
}

if (-not $iscc) {
    throw "Inno Setup 6 (ISCC.exe) was not found. Install from https://jrsoftware.org/isdl.php or: winget install JRSoftware.InnoSetup"
}

Write-Host "==> Using ISCC: $iscc" -ForegroundColor Green
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Write-Host "==> Compiling installer..." -ForegroundColor Cyan
& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }

$setup = Join-Path $outDir "ObaidPricing-Setup-$version.exe"
if (-not (Test-Path $setup)) {
    throw "Expected installer not found: $setup"
}

if ($env:OBAD_SIGN_CERT -and (Test-Path $env:OBAD_SIGN_CERT)) {
    Write-Host "==> Signing installer with OBAD_SIGN_CERT..." -ForegroundColor Cyan
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if (-not $signtool) {
        $signtoolCmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
        if ($signtoolCmd) { $signtool = $signtoolCmd }
    }
    if (-not $signtool) {
        Write-Warning "signtool.exe not found - installer left unsigned."
    }
    else {
        $toolPath = if ($signtool.FullName) { $signtool.FullName } else { $signtool.Source }
        if ($env:OBAD_SIGN_PASSWORD) {
            & $toolPath sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f $env:OBAD_SIGN_CERT /p $env:OBAD_SIGN_PASSWORD $setup
        }
        else {
            & $toolPath sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f $env:OBAD_SIGN_CERT $setup
        }
        if ($LASTEXITCODE -ne 0) { throw "signtool failed ($LASTEXITCODE)" }
        Write-Host "==> Installer signed." -ForegroundColor Green
    }
}
else {
    Write-Host "==> Skipping code signing (set OBAD_SIGN_CERT / OBAD_SIGN_PASSWORD to enable)." -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "Installer ready:" -ForegroundColor Green
Write-Host "  $setup"
Write-Host ""
Write-Host "Notes:"
Write-Host "  - App is self-contained: no separate .NET Desktop Runtime install is required."
Write-Host "  - User data remains in %LocalAppData%\CostWise\ and is not removed on uninstall."
Write-Host "  - Desktop shortcut is optional (unchecked by default in the wizard)."
