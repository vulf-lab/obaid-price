# User manual

OBAID Pricing helps manage fish-feed **formulations**, **nutrition**, **costing**, and **price lists** on Windows.

## Install and first launch

1. Run `ObaidPricing-win-Setup.exe` (from your IT team or GitHub Releases).
2. Start **OBAID Pricing** from the Start Menu.
3. On first run, **create a password** (min 8 characters). Remember it — there is no cloud reset.
4. Optional later: set a **PIN** on the Profile page for quicker unlock when “remember me” is on.

Data is stored on this PC under your Windows profile (`%LocalAppData%\CostWise\`). Uninstalling the app keeps that data.

## Sign in

- Enter password (or PIN if configured).
- After several wrong attempts, wait briefly and try again.

## Main navigation

Use the left sidebar:

| Page | What you do there |
|------|-------------------|
| **Formulations** | Browse/edit formulas, ingredients %, revisions, import/export |
| **Nutrition Profiles** | Spec targets / min / max by nutrient |
| **Comparison** | Side-by-side formulas or profiles; print/export |
| **Production** | Active production matrix / groups |
| **Raw Ingredients** | RM list, prices, availability, history |
| **Pricing** | Active pricing books and costing scenarios |
| **Price Lists** | Commercial and Victory PDF/Excel style lists |
| **Profile** | Change password, PIN, sign out |
| **Settings** | Masters (feed types, species, sizes…), cost options, currencies, display decimals, branding logos, **updates** |

## Everyday tips

- **Save** after editing grids or Settings sections that have an explicit Save button.
- Formulations typically expect ingredient inclusions to total **100%** for producibility rules.
- Price list PDFs use logos from Settings → Application → Branding.
- Decimal places for display are under Settings → Application → Display (stored values are unchanged).

## Updates

By default the app **asks** before downloading a new version (Settings → Application → Updates).

- **Update now** — download, restart, keep your data.
- **Later** — ask again another day.
- **Skip this version** — ignore that version until a newer one appears.

You can turn checks off or choose quieter download behavior in the same Settings section.

## Backup

Copy the folder `%LocalAppData%\CostWise\` while the app is closed. Ask IT for help if unsure. See [backup-and-restore.md](backup-and-restore.md).

## Getting help

If something fails, note the message and check logs under `%LocalAppData%\CostWise\logs\`, or contact your administrator with the app version shown under Settings → Updates.
