# Release checklist (1.0.0+)

Use before customer or wide internal distribution. Check items off as you go.

## Build and package

- [ ] Version bumped in csproj, pubxml, `build-velopack.ps1` / validate scripts (see [build-process.md](build-process.md))
- [ ] `dotnet test CostWise.sln -c Release` passes
- [ ] `installer\build-velopack.ps1` succeeds
- [ ] `installer\validate-velopack.ps1 -SkipBuild` passes
- [ ] Optional: sign Setup with `OBAD_SIGN_CERT`
- [ ] GitHub Release created; **all** `artifacts/velopack/` files uploaded; release published (not draft/prerelease for production)

## Clean-machine / install smoke (Velopack Setup)

- [ ] Install `ObaidPricing-win-Setup.exe`
- [ ] First-run password setup / login
- [ ] Sidebar + login branding visible
- [ ] Formulations list loads
- [ ] Nutrition Profiles loads
- [ ] Comparison opens with a selection
- [ ] Production matrix loads
- [ ] Raw Ingredients loads
- [ ] Pricing / Price Lists load
- [ ] Settings (Product / Pricing / Nutrition / Application) loads
- [ ] Profile (password / PIN / sign out)
- [ ] One Settings Add/Save smoke
- [ ] Optional: one PDF or Excel export
- [ ] Confirm `%LocalAppData%\CostWise\costwise.db` exists

## Update smoke (recommended for 1.0.1+)

- [ ] Install build N; create sample data
- [ ] Publish build N+1 to GitHub (or `COSTWISE_UPDATE_URL` local feed)
- [ ] Prompt appears → Update now
- [ ] App restarts on N+1; data still present
- [ ] `backups\pre-update-*.db` created

## Docs / comms

- [ ] [RELEASE_NOTES.md](RELEASE_NOTES.md) / [CHANGELOG.md](CHANGELOG.md) match this version
- [ ] Operators know SmartScreen / unsigned caveat if applicable
- [ ] Backup guidance shared ([backup-and-restore.md](backup-and-restore.md))

## Go / no-go

- [ ] **Go** only if install smoke is green and packaging/GitHub assets are correct  
- [ ] **Pilot only** if update E2E or signing still pending (document the gap)
