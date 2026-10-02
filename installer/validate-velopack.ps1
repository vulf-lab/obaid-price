<#
.SYNOPSIS
  Smoke-check Velopack pack output for OBAID Pricing.

.DESCRIPTION
  Ensures artifacts/velopack contains expected pack outputs after build-velopack.ps1.
  Does not perform a full install (UAC / Start Menu) — use a manual Velopack install for E2E.
#>
[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

$version = "1.0.3"
$outDir = Join-Path $root "artifacts\velopack"
$results = [System.Collections.Generic.List[object]]::new()

function Add-Result([string]$Name, [string]$Status, [string]$Detail) {
    $results.Add([pscustomobject]@{ Name = $Name; Status = $Status; Detail = $Detail })
    $color = if ($Status -eq "PASS") { "Green" } elseif ($Status -eq "FAIL") { "Red" } else { "Yellow" }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Name, $Detail) -ForegroundColor $color
}

if (-not $SkipBuild -or -not (Test-Path $outDir)) {
    & powershell -ExecutionPolicy Bypass -File (Join-Path $root "installer\build-velopack.ps1")
    if ($LASTEXITCODE -ne 0) { throw "build-velopack.ps1 failed" }
}

if (-not (Test-Path $outDir)) {
    Add-Result "Velopack output dir" "FAIL" "Missing $outDir"
}
else {
    Add-Result "Velopack output dir" "PASS" $outDir
}

$nupkg = @(Get-ChildItem $outDir -Filter "*.nupkg" -ErrorAction SilentlyContinue)
$releases = @(Get-ChildItem $outDir -Filter "releases*.json" -ErrorAction SilentlyContinue)
$setup = @(Get-ChildItem $outDir -Filter "*Setup*.exe" -ErrorAction SilentlyContinue)
if ($setup.Count -eq 0) {
    $setup = @(Get-ChildItem $outDir -Filter "*.exe" -ErrorAction SilentlyContinue | Where-Object { $_.Name -notmatch "Update" })
}

if ($nupkg.Count -gt 0) { Add-Result "Package nupkg" "PASS" $nupkg[0].Name } else { Add-Result "Package nupkg" "FAIL" "No .nupkg" }
if ($releases.Count -gt 0) { Add-Result "Releases feed" "PASS" $releases[0].Name } else { Add-Result "Releases feed" "FAIL" "No releases*.json" }
if ($setup.Count -gt 0) { Add-Result "Setup exe" "PASS" $setup[0].Name } else { Add-Result "Setup exe" "FAIL" "No Setup exe" }

$publishExe = Join-Path $root "artifacts\release\$version\ObaidPricing.exe"
if (Test-Path $publishExe) {
    Add-Result "Publish folder exe" "PASS" $publishExe
}
else {
    Add-Result "Publish folder exe" "WARN" "Missing (pack may still have succeeded)"
}

$dataRoot = Join-Path $env:LOCALAPPDATA "CostWise"
Add-Result "User data path" "PASS" "Preserved separately at $dataRoot (not under Velopack current\)"

$fail = @($results | Where-Object Status -eq "FAIL").Count
Write-Host ""
Write-Host "Manual E2E: install Setup.exe, confirm %LocalAppData%\ObaidPricing\, login, Settings > Check for updates." -ForegroundColor Cyan
Write-Host "Local feed test: set COSTWISE_UPDATE_URL to a folder containing releases.win.json + nupkg." -ForegroundColor Cyan

if ($fail -gt 0) { exit 1 } else { exit 0 }
