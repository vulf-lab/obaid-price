<#
.SYNOPSIS
  Publishes OBAID Pricing and packs a Velopack release (primary installer + update channel).

.DESCRIPTION
  Output under artifacts/velopack/ includes Setup.exe, nupkg packages, and releases.*.json
  for upload to GitHub Releases (https://github.com/vulf-lab/obaid-price).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File installer\build-velopack.ps1

.EXAMPLE
  powershell -File installer\build-velopack.ps1 -SkipPublish
#>
[CmdletBinding()]
param(
    [switch]$SkipPublish,
    [string]$ReleaseNotes = ""
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

$version = "1.0.3"
$packId = "ObaidPricing"
$publishDir = Join-Path $root "artifacts\release\$version"
$outDir = Join-Path $root "artifacts\velopack"
$icon = Join-Path $root "src\CostWise.App\Assets\obaid-pricing-logo.ico"
$mainExe = "ObaidPricing.exe"

Write-Host "==> OBAID Pricing Velopack build ($version)" -ForegroundColor Cyan

if (-not $SkipPublish) {
    Write-Host "==> Publishing folder Release (win-x64, self-contained)..." -ForegroundColor Cyan
    # Clean publish dir so stale single-file leftovers do not confuse vpk
    if (Test-Path $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }
    dotnet publish (Join-Path $root "src\CostWise.App\CostWise.App.csproj") `
        -p:PublishProfile=Win64Folder
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }
}

$exe = Join-Path $publishDir $mainExe
if (-not (Test-Path $exe)) {
    throw "Published exe not found: $exe. Run without -SkipPublish first."
}
if (-not (Test-Path $icon)) {
    throw "Pack icon not found: $icon"
}

Write-Host "==> Ensuring vpk tool..." -ForegroundColor Cyan
$vpk = Get-Command vpk -ErrorAction SilentlyContinue
if (-not $vpk) {
    dotnet tool update -g vpk
    if ($LASTEXITCODE -ne 0) {
        dotnet tool install -g vpk
    }
    $vpk = Get-Command vpk -ErrorAction SilentlyContinue
}
if (-not $vpk) {
    throw "vpk CLI not found. Install with: dotnet tool install -g vpk"
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$packArgs = @(
    "pack",
    "--packId", $packId,
    "--packVersion", $version,
    "--packDir", $publishDir,
    "--mainExe", $mainExe,
    "--packTitle", "OBAID Pricing",
    "--packAuthors", "OBAID",
    "--icon", $icon,
    "--outputDir", $outDir
)

if (-not [string]::IsNullOrWhiteSpace($ReleaseNotes)) {
    $notesFile = Join-Path $env:TEMP "obaid-release-notes-$version.md"
    Set-Content -Path $notesFile -Value $ReleaseNotes -Encoding UTF8
    $packArgs += @("--releaseNotes", $notesFile)
}

Write-Host "==> vpk $($packArgs -join ' ')" -ForegroundColor Cyan
& vpk @packArgs
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed ($LASTEXITCODE)" }

$setup = Get-ChildItem $outDir -Filter "*Setup*.exe" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if (-not $setup) {
    $setup = Get-ChildItem $outDir -Filter "*.exe" -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notmatch "Update" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

Write-Host ""
Write-Host "==> Velopack output: $outDir" -ForegroundColor Green
if ($setup) {
    Write-Host "    Setup: $($setup.FullName)" -ForegroundColor Green
}

if ($env:OBAD_SIGN_CERT -and (Test-Path $env:OBAD_SIGN_CERT) -and $setup) {
    Write-Host "==> Signing Velopack setup with OBAD_SIGN_CERT..." -ForegroundColor Cyan
    $signtool = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe",
        "${env:ProgramFiles}\Windows Kits\10\bin\*\x64\signtool.exe"
    ) | ForEach-Object { Get-Item $_ -ErrorAction SilentlyContinue } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($signtool) {
        $signArgs = @("sign", "/fd", "SHA256", "/tr", "http://timestamp.digicert.com", "/td", "SHA256", "/f", $env:OBAD_SIGN_CERT)
        if ($env:OBAD_SIGN_PASSWORD) { $signArgs += @("/p", $env:OBAD_SIGN_PASSWORD) }
        $signArgs += $setup.FullName
        & $signtool.FullName @signArgs
    }
    else {
        Write-Warning "signtool.exe not found; skipping signing."
    }
}

Write-Host ""
Write-Host "Next: upload ALL files from artifacts\velopack\ to a GitHub Release tag (e.g. v$version)." -ForegroundColor Cyan
Write-Host "  Repo: https://github.com/vulf-lab/obaid-price/releases" -ForegroundColor Cyan
Write-Host "  Clients check via GithubSource; do not rename release assets." -ForegroundColor Cyan
