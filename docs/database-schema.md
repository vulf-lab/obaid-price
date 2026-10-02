# Database schema

**Engine:** SQLite via EF Core 8  
**Default path:** `%LocalAppData%\CostWise\costwise.db`  
**At rest:** Not encrypted (UI login does not encrypt the file)

## Initialization

On every app start ([`DependencyInjection.InitializeDatabaseAsync`](../src/CostWise.Infrastructure/DependencyInjection.cs)):

1. `Database.MigrateAsync()` — apply pending EF migrations  
2. `DbSeeder.SeedAsync` — idempotent reference data  
3. Feed type / spec parameter normalizers  
4. Optional orphan cleanup if `COSTWISE_RUN_ORPHAN_CLEANUP=1`  
5. `SystemIdGenerator.EnsureAllAssignedAsync`

## Tables (entities)

| Table / entity | Key fields | Notes |
|----------------|------------|-------|
| `FeedTypes` | Name (unique), IsActive | Masters |
| `Species` | Name (unique), IsActive | Masters |
| `Categories` | Name (unique), IsActive | Masters |
| `SubCategories` | Name (unique), IsActive | Masters |
| `Sizes` | Name, DiameterMm, ConversionCost, FeedTypeId? | FeedType SetNull |
| `RawIngredients` | Name (unique), PricePerMt, IsAvailable | |
| `RawIngredientPriceHistories` | PricePerMt, rates, ChangedAtUtc | Cascade from ingredient |
| `Currencies` | Code (unique), KesPerUnit, IsBase, SortOrder | |
| `SpecParameters` | Name (unique), Unit | Nutrition |
| `Formulations` | SystemId, Code (unique), Name, Revision, FKs, IsActive | Core product |
| `FormulationIngredients` | InclusionPercent | Unique (Formulation, RawIngredient) |
| `FormulationSpecs` | Target / Min / Max | Unique (Formulation, SpecParameter) |
| `FormulationChangeLogs` | Action, Summary, Details | Audit |
| `PricingCostOptions` | Name, Cost, Kind | Packing / Documents / Additive |
| `CostingScenarios` | Market, costs, margin, option FKs | Per formulation |
| `PriceBooks` | Name, costs, margin, PriceUnit, rounding, currency | |
| `PriceBookFormulations` | SortOrder, override sells | Join |
| `ProductionGroups` | Name, SortOrder | |
| `ProductionGroupFormulations` | SortOrder | Join |
| `CommercialPriceLists` | Name, Currency, SellUnit, EffectiveDate | |
| `CommercialPriceListBooks` | SortOrder | Join; one list per book |
| `VictoryReportSnapshots` | Label, BookA/B, margins, BriefJson | Archived brief omits logos |
| `VictoryReportSnapshotLines` | BookRole, FormulationCode, FeedTypeName, SizeName, Sell Mt/Bag, CurrencyCode | Formula/book FKs SetNull; Last Price matches feed type + size |

Defined in [`CostWiseDbContext`](../src/CostWise.Infrastructure/Data/CostWiseDbContext.cs).

## Enums (Core)

| Enum | Values | Used by |
|------|--------|---------|
| `MarketType` | Domestic, Export | CostingScenario |
| `PriceUnit` | PerMt, PerBag25Kg | PriceBook, CommercialPriceList |
| `PricingCostKind` | Packing, Documents, Additive | PricingCostOption |
| `FormulationChangeAction` | Created, Imported, Updated, Deleted | FormulationChangeLog |

## Migration history (chronological)

| Migration | Purpose |
|-----------|---------|
| `InitialCreate` | Masters + formulations + ingredients/specs/costing |
| `AddSubCategory` | SubCategories |
| `AddFormulationRevision` | Revision |
| `AddSystemIdAndAudit` | SystemId, change logs, timestamps |
| `AddFormulationIsActive` | IsActive |
| `AddPriceBooks` | Price books |
| `AddProductionGroups` | Production groups |
| `AddProductionGroupSortOrder` | Group sort |
| `AddPricingCostOptions` | Cost option catalog |
| `AddPriceBookFormulations` | Book ↔ formula |
| `AddPriceBookRoundingAndSellOverrides` | Rounding + overrides |
| `AddPriceBookSortOrder` | Book sort |
| `AddRawIngredientPriceHistoryAndRenameFinisher` | Price history |
| `AddCurrenciesAndPriceBookDisplayCurrency` | Currencies |
| `AddCommercialPriceLists` | Commercial lists |
| `AddVictoryReportSnapshots` | Victory snapshots |
| `AddVictorySnapshotFormulaCode` | Formula code, book side, archived brief |
| `AddVictorySnapshotProduct` | Feed type and size on snapshot lines |

Source: `src/CostWise.Infrastructure/Data/Migrations/`.

## Upgrade safety

- New app versions carry new migrations; first launch after update applies them.
- Pre-update DB copies live in `CostWise\backups\` (see [backup-and-restore.md](backup-and-restore.md)).
- Migrations are **forward-only**; rolling back the app binary may require restoring a backup if the schema already advanced.
