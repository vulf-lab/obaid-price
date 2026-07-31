# Troubleshooting

## Startup / database

| Symptom | Likely cause | Action |
|---------|--------------|--------|
| “Startup failed” MessageBox | Exception during host/DB/login | Open `%LocalAppData%\CostWise\logs\costwise-yyyyMMdd.log` |
| DB upgrade failed after update | Migration error | Choose restore from `backups\`, or reinstall previous release |
| Empty / missing `costwise.db` | First run aborted early | Relaunch; complete login after migrate |
| Orphan data surprises | Cleanup env var | Only if `COSTWISE_RUN_ORPHAN_CLEANUP=1` was set |

## Login / account

| Symptom | Action |
|---------|--------|
| Locked out | Wait for cooldown (~30s after 5 failures), then retry |
| “Damaged” account | Corrupt `user-account.dat` — restore from backup or delete file to force setup (does not delete DB) |
| Wrong machine after restore | DPAPI: recreate password on new Windows user; DB can still load |

## Updates (Velopack)

| Symptom | Action |
|---------|--------|
| “Only from a Velopack install” | Running via `dotnet run` / VS — install Setup.exe to test updates |
| No update found | Confirm GitHub Release assets + `releases.win.json`; version must be higher |
| Download fails | Network / rate limit; try again; check logs |
| Want local feed | `$env:COSTWISE_UPDATE_URL = "C:\path\to\artifacts\velopack"` |

## Installer

| Symptom | Action |
|---------|--------|
| SmartScreen warning | Expected if unsigned; sign or user override |
| Inno needs admin | Legacy machine-wide install; prefer Velopack for per-user |
| Data missing after uninstall | Uninstall should **keep** CostWise folder — check `%LocalAppData%\CostWise\` |

## CLI tools

| Symptom | Action |
|---------|--------|
| Exit code 2 | Missing `--db <path>` or `--force-local` (tools refuse silent LocalAppData use) |
| Wrong database updated | Pass explicit `--db` path |

Example:

```powershell
dotnet run --project tools/CostWise.Import -- "file.xlsx" --db "$env:LOCALAPPDATA\CostWise\costwise.db"
```

## Display / export

| Symptom | Action |
|---------|--------|
| PDF export crash | Check logs; QuestPDF/Skia — ensure folder publish assets intact |
| Missing logos on PDF | Settings → Application → Branding uploads |

## Getting help

Capture: app version (Settings → Updates), OS build, log file excerpt, whether install is Velopack or Inno.
