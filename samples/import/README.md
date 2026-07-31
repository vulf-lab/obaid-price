# Import sample formats

Exact Excel templates matching the in-app Import parsers. Prefer **Sample** next to **Import** in the app (always matches current headers).

| File | Used by |
|------|---------|
| `raw-material-price-import-sample.xlsx` | Raw Ingredients → Import |
| `formulation-import-sample.xlsx` | Formulations → Import |
| `nutrition-profile-import-sample.xlsx` | Nutrition Profiles → Import |

Regenerate:

```bash
dotnet run --project tools/CostWise.WriteImportSamples -- samples/import
```
