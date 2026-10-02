<#
.SYNOPSIS
  Clean-install release validation for OBAID Pricing (simulated new machine).

.DESCRIPTION
  Isolates %LocalAppData%\CostWise, silent-installs the Inno setup, verifies launch/DB/logs,
  uninstalls (preserving user data), restores LocalAppData, and writes a validation report.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File installer\validate-release.ps1

.EXAMPLE
  powershell -File installer\validate-release.ps1 -SkipBuild
#>
[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

$version = "1.0.4"
$setup = Join-Path $root "artifacts\installer\ObaidPricing-Setup-$version.exe"
$installDir = Join-Path $env:LOCALAPPDATA "ObaidPricing-ReleaseValidate\App"
$costWiseData = Join-Path $env:LOCALAPPDATA "CostWise"
$backupData = Join-Path $env:LOCALAPPDATA "CostWise.__release_backup__"
$reportDir = Join-Path $root "artifacts\release"
$reportPath = Join-Path $reportDir "RELEASE_VALIDATION_REPORT.md"
$results = [System.Collections.Generic.List[object]]::new()
$started = Get-Date
$backupTaken = $false
$installed = $false

function Add-Result([string]$Name, [string]$Status, [string]$Detail) {
    $results.Add([pscustomobject]@{ Name = $Name; Status = $Status; Detail = $Detail })
    $color = switch ($Status) {
        "PASS" { "Green" }
        "FAIL" { "Red" }
        "WARN" { "Yellow" }
        default { "Gray" }
    }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Name, $Detail) -ForegroundColor $color
}

function Restore-UserData {
    if (-not $backupTaken) { return }
    try {
        if (Test-Path $costWiseData) {
            Remove-Item -LiteralPath $costWiseData -Recurse -Force -ErrorAction SilentlyContinue
        }
        if (Test-Path $backupData) {
            Rename-Item -LiteralPath $backupData -NewName "CostWise"
            Write-Host "==> Restored LocalAppData\CostWise from backup." -ForegroundColor Cyan
        }
    }
    catch {
        Write-Warning "Failed to restore LocalAppData backup: $_"
    }
}

try {
    Write-Host "==> OBAID Pricing release validation" -ForegroundColor Cyan
    Write-Host "    Root: $root"

    if (-not $SkipBuild -or -not (Test-Path $setup)) {
        Write-Host "==> Ensuring installer exists..." -ForegroundColor Cyan
        $buildScript = Join-Path $root "installer\build-installer.ps1"
        $extra = @()
        if (Test-Path (Join-Path $root "artifacts\release\$version\ObaidPricing.exe")) {
            $extra += "-SkipPublish"
        }
        & powershell -ExecutionPolicy Bypass -File $buildScript @extra
        if ($LASTEXITCODE -ne 0) { throw "build-installer.ps1 failed ($LASTEXITCODE)" }
    }

    if (-not (Test-Path $setup)) {
        Add-Result "Installer present" "FAIL" "Missing $setup"
        throw "Installer not found."
    }
    $setupInfo = Get-Item $setup
    Add-Result "Installer present" "PASS" ("{0} ({1:N1} MB)" -f $setupInfo.FullName, ($setupInfo.Length / 1MB))

    # Isolate existing user data for first-run simulation
    if (Test-Path $backupData) {
        Remove-Item -LiteralPath $backupData -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $costWiseData) {
        Rename-Item -LiteralPath $costWiseData -NewName "CostWise.__release_backup__"
        $backupTaken = $true
        Add-Result "LocalAppData isolated" "PASS" "Renamed CostWise -> CostWise.__release_backup__"
    }
    else {
        Add-Result "LocalAppData isolated" "PASS" "No existing CostWise folder (already clean)"
    }

    # Clean previous validation install
    if (Test-Path $installDir) {
        $unins = Get-ChildItem $installDir -Filter "unins*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($unins) {
            Start-Process -FilePath $unins.FullName -ArgumentList "/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART" -Wait -NoNewWindow
        }
        Remove-Item -LiteralPath (Split-Path $installDir) -Recurse -Force -ErrorAction SilentlyContinue
    }

    New-Item -ItemType Directory -Force -Path $installDir | Out-Null

    Write-Host "==> Silent install..." -ForegroundColor Cyan
    $installArgs = @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/DIR=$installDir",
        "/TASKS=",
        "/CLOSEAPPLICATIONS"
    )
    $p = Start-Process -FilePath $setup -ArgumentList $installArgs -Wait -PassThru -NoNewWindow
    if ($p.ExitCode -ne 0) {
        Add-Result "Silent install" "FAIL" "Setup exit code $($p.ExitCode)"
        throw "Silent install failed."
    }
    $installed = $true

    $appExe = Join-Path $installDir "ObaidPricing.exe"
    if (-not (Test-Path $appExe)) {
        Add-Result "Silent install" "FAIL" "ObaidPricing.exe missing under $installDir"
        throw "Install incomplete."
    }
    Add-Result "Silent install" "PASS" "Installed to $installDir"
    Add-Result "Application files" "PASS" "ObaidPricing.exe present (embedded pack:// assets)"

    # Launch
    Write-Host "==> Launching application..." -ForegroundColor Cyan
    $proc = Start-Process -FilePath $appExe -WorkingDirectory $installDir -PassThru
    Start-Sleep -Seconds 12
    if ($proc.HasExited) {
        Add-Result "Application launch" "FAIL" "Process exited early code=$($proc.ExitCode)"
    }
    else {
        Add-Result "Application launch" "PASS" "Process running pid=$($proc.Id) after 12s"
        try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch { }
        Start-Sleep -Seconds 2
    }

    # DB
    $dbPath = Join-Path $costWiseData "costwise.db"
    if ((Test-Path $dbPath) -and ((Get-Item $dbPath).Length -gt 0)) {
        Add-Result "Database initialize" "PASS" ("{0} ({1:N0} bytes)" -f $dbPath, (Get-Item $dbPath).Length)
    }
    else {
        # First-run may block on login/setup UI before migrate finishes; wait a bit longer with relaunch
        Write-Host "==> DB not yet present; relaunching briefly for migrate..." -ForegroundColor Yellow
        $proc2 = Start-Process -FilePath $appExe -WorkingDirectory $installDir -PassThru
        Start-Sleep -Seconds 20
        if (-not $proc2.HasExited) { Stop-Process -Id $proc2.Id -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
        if ((Test-Path $dbPath) -and ((Get-Item $dbPath).Length -gt 0)) {
            Add-Result "Database initialize" "PASS" ("{0} ({1:N0} bytes) after extended launch" -f $dbPath, (Get-Item $dbPath).Length)
        }
        else {
            Add-Result "Database initialize" "FAIL" "costwise.db missing or empty after launch"
        }
    }

    # Logging — AppLog writes on errors; probe by ensuring directory can be created and documenting behavior
    $logDir = Join-Path $costWiseData "logs"
    if (Test-Path $logDir) {
        $logFiles = @(Get-ChildItem $logDir -Filter "*.log" -ErrorAction SilentlyContinue)
        if ($logFiles.Count -gt 0) {
            Add-Result "Logging" "PASS" ("Log files present: {0}" -f ($logFiles | Select-Object -First 1).Name)
        }
        else {
            Add-Result "Logging" "WARN" "logs\ folder exists but no file yet (AppLog writes on errors/startup failures)"
        }
    }
    else {
        # Force a log write by invoking a tiny helper if possible — otherwise document
        New-Item -ItemType Directory -Force -Path $logDir | Out-Null
        $probe = Join-Path $logDir ("validation-probe-{0:yyyyMMdd}.log" -f (Get-Date))
        "validation probe $(Get-Date -Format o)" | Set-Content -Path $probe -Encoding UTF8
        if (Test-Path $probe) {
            Add-Result "Logging" "WARN" "App did not create logs\ on clean launch (expected until error). Path is writable: $logDir"
            Remove-Item $probe -Force -ErrorAction SilentlyContinue
        }
        else {
            Add-Result "Logging" "FAIL" "Cannot write under $logDir"
        }
    }

    # Uninstall
    Write-Host "==> Uninstalling..." -ForegroundColor Cyan
    $uninstaller = Get-ChildItem $installDir -Filter "unins*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $uninstaller) {
        Add-Result "Uninstall" "FAIL" "unins*.exe not found in install dir"
    }
    else {
        $u = Start-Process -FilePath $uninstaller.FullName -ArgumentList "/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART" -Wait -PassThru -NoNewWindow
        Start-Sleep -Seconds 3
        $exeGone = -not (Test-Path $appExe)
        $dataKept = Test-Path $dbPath
        if ($u.ExitCode -eq 0 -and $exeGone -and $dataKept) {
            Add-Result "Uninstall preserves data" "PASS" "App removed; LocalAppData CostWise DB retained"
            Add-Result "Uninstall clean" "PASS" "Installer uninstall exit 0; ObaidPricing.exe removed"
        }
        elseif ($u.ExitCode -eq 0 -and $exeGone -and -not $dataKept) {
            Add-Result "Uninstall clean" "PASS" "App removed"
            Add-Result "Uninstall preserves data" "FAIL" "LocalAppData costwise.db was removed (should preserve)"
        }
        else {
            Add-Result "Uninstall clean" "FAIL" "exit=$($u.ExitCode) exeGone=$exeGone dataKept=$dataKept"
        }
    }
    $installed = $false

    # Cleanup validation install root
    Remove-Item -LiteralPath (Split-Path $installDir) -Recurse -Force -ErrorAction SilentlyContinue
}
catch {
    Add-Result "Validation aborted" "FAIL" $_.Exception.Message
}
finally {
    # If still installed, try uninstall
    if ($installed -and (Test-Path $installDir)) {
        $uninstaller = Get-ChildItem $installDir -Filter "unins*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($uninstaller) {
            Start-Process -FilePath $uninstaller.FullName -ArgumentList "/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART" -Wait -NoNewWindow -ErrorAction SilentlyContinue
        }
        Remove-Item -LiteralPath (Split-Path $installDir) -Recurse -Force -ErrorAction SilentlyContinue
    }
    Restore-UserData
}

# Write report
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$os = [System.Environment]::OSVersion.VersionString
$elapsed = (Get-Date) - $started
$pass = @($results | Where-Object Status -eq "PASS").Count
$fail = @($results | Where-Object Status -eq "FAIL").Count
$warn = @($results | Where-Object Status -eq "WARN").Count
$overall = if ($fail -gt 0) { "FAIL" } elseif ($warn -gt 0) { "PASS WITH WARNINGS" } else { "PASS" }

$rows = ($results | ForEach-Object { "| $($_.Name) | $($_.Status) | $($_.Detail) |" }) -join "`n"

$report = @"
# OBAID Pricing - Release Validation Report

| Field | Value |
|-------|-------|
| Date (UTC) | $((Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")) |
| Machine | $env:COMPUTERNAME |
| OS | $os |
| App version | $version |
| Installer | ``$setup`` |
| Installer size | $([math]::Round((Get-Item $setup -ErrorAction SilentlyContinue).Length / 1MB, 1)) MB |
| Duration | $([int]$elapsed.TotalSeconds)s |
| Overall | **$overall** ($pass pass / $warn warn / $fail fail) |

## Automated checks

| Check | Status | Detail |
|-------|--------|--------|
$rows

## Simulation notes

- Clean first-run simulated by temporarily renaming ``%LocalAppData%\CostWise``.
- Silent install to ``$installDir`` (per-user path under LocalAppData for non-elevated validation when possible; setup may still request elevation).
- Application UI feature coverage is **manual** (see below). Automated checks cover install, launch stability, DB creation, logging path, and uninstall data preservation.

## Manual feature checklist (operator)

Run after installing once for interactive smoke (not executed by this script):

- [ ] First-run password setup / login
- [ ] Sidebar logo and login branding assets visible
- [ ] Formulations list loads
- [ ] Nutrition Profiles loads
- [ ] Comparison opens with selection
- [ ] Production matrix loads
- [ ] Raw Ingredients loads
- [ ] Pricing / Price Lists load
- [ ] Settings (Product / Pricing / Nutrition / Application) loads
- [ ] Profile page (password / PIN / sign out)
- [ ] One Settings Add/Save smoke
- [ ] Optional: one PDF/Excel export

## Remaining issues / known residuals

1. **Installer is unsigned** - SmartScreen may warn until ``OBAD_SIGN_CERT`` signing is configured.
2. **SQLite database is not encrypted at rest** - UI login gates the app; file copy of ``costwise.db`` bypasses login (documented threat-model residual).
3. **Logging is error-driven** - ``AppLog`` writes under ``%LocalAppData%\CostWise\logs`` on failures; clean launches may not create a log file (path is writable).
4. **No automated UI tests** for every navigation page - complete the manual checklist before customer distribution.
5. Earlier hardening residuals (not blockers for this install validation): god ViewModels, no EF concurrency tokens, repository layer not introduced.

## Conclusion

Automated release validation overall: **$overall**.
"@

Set-Content -Path $reportPath -Value $report -Encoding UTF8
Write-Host ""
Write-Host "Report written: $reportPath" -ForegroundColor Cyan
Write-Host "Overall: $overall" -ForegroundColor $(if ($fail -gt 0) { "Red" } else { "Green" })

if ($fail -gt 0) { exit 1 } else { exit 0 }
