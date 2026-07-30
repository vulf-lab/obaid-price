# CostWise

Windows WPF app for fish feed formulations, costing, and pricing.

## Requirements

- .NET 8 SDK
- Windows 10/11

## Run

```bash
dotnet run --project src/CostWise.App
```

Database path: `%LocalAppData%\CostWise\costwise.db`

## Import formulations from Excel

```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
dotnet run --project tools/CostWise.Import -- "c:\Users\oureh\OneDrive\Documents\Copy of CostwiseF.xlsx"
```

Expected columns: Category, Size, Specie, Feed Type, Code, Rev, Sub-Category, RM/Description, %
