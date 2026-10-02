using System.IO;
using System.Text;
using CostWise.App.Services;
using CostWise.Core.Entities;
using CostWise.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace CostWise.App.Tests;

public class VictorySellPriceCompareTests
{
    [Fact]
    public void Same_feed_type_and_size_matches_when_code_changed()
    {
        var rows = new[] { Row(id: 99, code: "NEW-CODE", sell: 110m, feed: "Pre-grower", size: "2mm") };
        var lines = new[]
        {
            Line(bookId: 3, formulaId: 7, role: "A", code: "OLD-CODE", sell: 100m, feed: "Pre-grower", size: "2mm")
        };

        var applied = VictorySellPriceCompare.Apply(rows, currentBookId: 3, snapshotBookId: 3, "A", lines);

        Assert.Equal(100m, applied[0].LastSellMt);
        Assert.Equal(10.0m, applied[0].SellChangePercent);
    }

    [Fact]
    public void Different_size_does_not_match_even_when_code_matches()
    {
        var rows = new[] { Row(id: 9, code: "SP4LV738-5", sell: 70m, feed: "Grower", size: "3mm") };
        var lines = new[]
        {
            Line(bookId: 3, formulaId: 1, role: "A", code: "SP4LV738-5", sell: 100m, feed: "Grower", size: "4mm")
        };

        var applied = VictorySellPriceCompare.Apply(rows, currentBookId: 3, snapshotBookId: 3, "A", lines);

        Assert.Null(applied[0].LastSellMt);
        Assert.Null(applied[0].SellChangePercent);
    }

    [Fact]
    public void Falls_back_to_code_when_snapshot_has_no_product()
    {
        var rows = new[] { Row(id: 99, code: "SP4LV738-5", sell: 80m, feed: "Grower", size: "4mm") };
        var lines = new[]
        {
            Line(bookId: 3, formulaId: 7, role: "A", code: "sp4lv738-5", sell: 100m)
        };

        var applied = VictorySellPriceCompare.Apply(rows, currentBookId: 3, snapshotBookId: 3, "A", lines);

        Assert.Equal(100m, applied[0].LastSellMt);
        Assert.Equal(-20.0m, applied[0].SellChangePercent);
    }

    [Fact]
    public void Matches_by_formulation_id_on_the_current_book()
    {
        var rows = new[] { Row(id: 7, code: "SP4LV738-5", sell: 110m) };
        var lines = new[]
        {
            Line(bookId: 3, formulaId: 7, role: "A", code: "SP4LV738-5", sell: 100m),
            Line(bookId: 4, formulaId: 7, role: "B", code: "SP4LV738-5", sell: 50m)
        };

        var applied = VictorySellPriceCompare.Apply(rows, currentBookId: 3, snapshotBookId: 3, "A", lines);

        Assert.Equal(100m, applied[0].LastSellMt);
        Assert.Equal(10.0m, applied[0].SellChangePercent);
    }

    [Fact]
    public void Matches_by_code_when_formulation_id_changed()
    {
        var rows = new[] { Row(id: 99, code: "SP4LV738-5", sell: 80m) };
        var lines = new[]
        {
            Line(bookId: 3, formulaId: 7, role: "A", code: "sp4lv738-5", sell: 100m)
        };

        var applied = VictorySellPriceCompare.Apply(rows, currentBookId: 3, snapshotBookId: 3, "A", lines);

        Assert.Equal(100m, applied[0].LastSellMt);
        Assert.Equal(-20.0m, applied[0].SellChangePercent);
    }

    [Fact]
    public void Matches_by_book_role_when_price_book_id_changed()
    {
        var rows = new[] { Row(id: 99, code: "SZV739-1", sell: 90m) };
        var lines = new[]
        {
            Line(bookId: 5, formulaId: 1, role: "A", code: "SZV739-1", sell: 100m),
            Line(bookId: 6, formulaId: 2, role: "B", code: "SZV739-1", sell: 40m)
        };

        var applied = VictorySellPriceCompare.Apply(rows, currentBookId: 80, snapshotBookId: 5, "A", lines);

        Assert.Equal(100m, applied[0].LastSellMt);
    }

    [Fact]
    public void No_match_leaves_last_price_null()
    {
        var rows = new[] { Row(id: 9, code: "NEW-1", sell: 70m) };
        var lines = new[] { Line(bookId: 3, formulaId: 1, role: "A", code: "OLD-1", sell: 100m) };

        var applied = VictorySellPriceCompare.Apply(rows, currentBookId: 3, snapshotBookId: 3, "A", lines);

        Assert.Null(applied[0].LastSellMt);
        Assert.Null(applied[0].SellChangePercent);
    }

    [Fact]
    public void Zero_or_missing_last_price_does_not_invent_a_change_percent()
    {
        var rows = new[]
        {
            Row(id: 1, code: "A", sell: 50m),
            Row(id: 2, code: "B", sell: 50m)
        };
        var lines = new[]
        {
            Line(bookId: 3, formulaId: 1, role: "A", code: "A", sell: 0m),
            Line(bookId: 3, formulaId: 2, role: "A", code: "B", sell: null)
        };

        var applied = VictorySellPriceCompare.Apply(rows, currentBookId: 3, snapshotBookId: 3, "A", lines);

        Assert.Equal(0m, applied[0].LastSellMt);
        Assert.Null(applied[0].SellChangePercent);
        Assert.Null(applied[1].LastSellMt);
        Assert.Null(applied[1].SellChangePercent);
    }

    private static PriceListBookRow Row(
        int id, string code, decimal? sell, string feed = "Feed", string size = "2mm") =>
        new(id, code, "Cat", feed, "Sub", size, true,
            1m, 1m, 1m, 1m, 1m, 10m, sell, 4m, null, null, null, null, "Book", "KES");

    private static VictoryReportSnapshotLine Line(
        int? bookId, int? formulaId, string role, string code, decimal? sell,
        string feed = "", string size = "") =>
        new()
        {
            PriceBookId = bookId,
            FormulationId = formulaId,
            BookRole = role,
            FormulationCode = code,
            FeedTypeName = feed,
            SizeName = size,
            SellMt = sell,
            CurrencyCode = "KES"
        };
}

public class VictoryBriefArchiveTests
{
    [Fact]
    public void Round_trips_commentary_rows_and_sells_without_logo_bytes()
    {
        var logo = new byte[] { 1, 2, 3, 4, 9, 8, 7 };
        var brief = new VictoryGroupBrief(
            "Victory Farms — KES per MT",
            "Kivu Choice — USD per MT",
            [
                new PriceListBookRow(
                    4, "SP4LV738-5", "Grower", "Sinking", "Balanced", "4mm", true,
                    67m, 7m, 1.5m, 0m, 0m, 76m, 89.2m, null, null, null, null, null,
                    "Victory Farms", "KES", 80m, 11.5m)
            ],
            [],
            [new RmPriceChangeRow("SOYA BEAN MEAL", 95.95m, 95.445m, -0.5m)],
            "Supply chain normalizing.",
            ["Code", "Sell / MT"],
            logo,
            logo,
            0m,
            0m,
            14.5m,
            14.5m,
            14.5m,
            null,
            true,
            0m);

        var json = VictoryBriefArchive.Serialize(brief);

        Assert.DoesNotContain(Convert.ToBase64String(logo), json);
        Assert.DoesNotContain(Encoding.UTF8.GetString(logo), json);

        var restored = VictoryBriefArchive.Deserialize(json, new byte[] { 5 }, new byte[] { 6 });
        Assert.NotNull(restored);
        Assert.Equal("Supply chain normalizing.", restored.Commentary);
        Assert.Equal("SP4LV738-5", restored.BookARows[0].Code);
        Assert.Equal(89.2m, restored.BookARows[0].SellMt);
        Assert.Equal(80m, restored.BookARows[0].LastSellMt);
        Assert.Equal("SOYA BEAN MEAL", restored.RmChanges[0].Name);
        Assert.Equal(new byte[] { 5 }, restored.LogoLeft);
        Assert.Equal(new byte[] { 6 }, restored.LogoRight);
        Assert.True(restored.ShowSellCompare);
    }
}

public class VictorySnapshotMigrationTests
{
    [Fact]
    public void Migration_backfills_formula_code_and_book_role()
    {
        var path = Path.Combine(Path.GetTempPath(), $"costwise-snap-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<CostWiseDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;

            using (var db = new CostWiseDbContext(options))
            {
                var migrator = db.Database.GetInfrastructure().GetService<IMigrator>()
                    ?? throw new InvalidOperationException("Migrator was not registered.");
                migrator.Migrate("20260730120000_AddVictoryReportSnapshots");

                using var connection = db.Database.GetDbConnection();
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO Categories (Name, IsActive) VALUES ('Cat', 1);
                    INSERT INTO SubCategories (Name, IsActive) VALUES ('Sub', 1);
                    INSERT INTO FeedTypes (Name, IsActive) VALUES ('Feed', 1);
                    INSERT INTO Species (Name, IsActive) VALUES ('Specie', 1);
                    INSERT INTO Sizes (Name, DiameterMm, ConversionCost, FeedTypeId, IsActive)
                    VALUES ('2mm', '2', '1', 1, 1);
                    INSERT INTO Formulations (
                        SystemId, Code, Name, Revision, IsActive,
                        FeedTypeId, SpeciesId, SizeId, CategoryId, SubCategoryId,
                        CreatedAtUtc, UpdatedAtUtc)
                    VALUES (
                        'SYS1', 'SP4LV738-5', 'Formula', '1', 1,
                        1, 1, 1, 1, 1,
                        '2026-08-21', '2026-08-21');
                    INSERT INTO PriceBooks (
                        Name, MarginPercent, PackingCost, ExportDocCost, SpecialAdditiveCost,
                        TransportationCost, PriceUnit, SortOrder, RoundMtTo, RoundBagTo, CreatedAtUtc, UpdatedAtUtc)
                    VALUES ('Victory Farms', '14.5', '0', '0', '0', '0', 0, 0, '0', '0', '2026-08-21', '2026-08-21');
                    INSERT INTO PriceBooks (
                        Name, MarginPercent, PackingCost, ExportDocCost, SpecialAdditiveCost,
                        TransportationCost, PriceUnit, SortOrder, RoundMtTo, RoundBagTo, CreatedAtUtc, UpdatedAtUtc)
                    VALUES ('Kivu Choice', '14.5', '0', '0', '0', '0', 0, 1, '0', '0', '2026-08-21', '2026-08-21');
                    INSERT INTO VictoryReportSnapshots (
                        CreatedAtUtc, Label, BookAId, BookBId, BookAMarginPercent, BookBMarginPercent, Note)
                    VALUES ('2026-08-21', 'Aug brief', 1, 2, '14.5', '14.5', 'Download PDF');
                    INSERT INTO VictoryReportSnapshotLines (
                        SnapshotId, PriceBookId, FormulationId, SellMt, SellBag, CurrencyCode)
                    VALUES (1, 2, 1, '695', '17', 'USD');
                    """;
                cmd.ExecuteNonQuery();
            }

            using (var db = new CostWiseDbContext(options))
            {
                db.Database.Migrate();
                var line = db.VictoryReportSnapshotLines.Single();
                Assert.Equal("SP4LV738-5", line.FormulationCode);
                Assert.Equal("B", line.BookRole);
                Assert.Equal("Feed", line.FeedTypeName);
                Assert.Equal("2mm", line.SizeName);
                Assert.Null(db.VictoryReportSnapshots.Single().BriefJson);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Migration_backfills_product_from_formula_code_when_id_is_missing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"costwise-snap-code-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<CostWiseDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;

            using (var db = new CostWiseDbContext(options))
            {
                var migrator = db.Database.GetInfrastructure().GetService<IMigrator>()
                    ?? throw new InvalidOperationException("Migrator was not registered.");
                migrator.Migrate("20261002091318_AddVictorySnapshotFormulaCode");

                using var connection = db.Database.GetDbConnection();
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO Categories (Name, IsActive) VALUES ('Cat', 1);
                    INSERT INTO SubCategories (Name, IsActive) VALUES ('Sub', 1);
                    INSERT INTO FeedTypes (Name, IsActive) VALUES ('Pre-grower', 1);
                    INSERT INTO Species (Name, IsActive) VALUES ('Specie', 1);
                    INSERT INTO Sizes (Name, DiameterMm, ConversionCost, FeedTypeId, IsActive)
                    VALUES ('2mm', '2', '1', 1, 1);
                    INSERT INTO Formulations (
                        SystemId, Code, Name, Revision, IsActive,
                        FeedTypeId, SpeciesId, SizeId, CategoryId, SubCategoryId,
                        CreatedAtUtc, UpdatedAtUtc)
                    VALUES (
                        'SYS1', 'OLD-CODE', 'Formula', '1', 1,
                        1, 1, 1, 1, 1,
                        '2026-08-21', '2026-08-21');
                    INSERT INTO PriceBooks (
                        Name, MarginPercent, PackingCost, ExportDocCost, SpecialAdditiveCost,
                        TransportationCost, PriceUnit, SortOrder, RoundMtTo, RoundBagTo, CreatedAtUtc, UpdatedAtUtc)
                    VALUES ('Victory Farms', '14.5', '0', '0', '0', '0', 0, 0, '0', '0', '2026-08-21', '2026-08-21');
                    INSERT INTO PriceBooks (
                        Name, MarginPercent, PackingCost, ExportDocCost, SpecialAdditiveCost,
                        TransportationCost, PriceUnit, SortOrder, RoundMtTo, RoundBagTo, CreatedAtUtc, UpdatedAtUtc)
                    VALUES ('Kivu Choice', '14.5', '0', '0', '0', '0', 0, 1, '0', '0', '2026-08-21', '2026-08-21');
                    INSERT INTO VictoryReportSnapshots (
                        CreatedAtUtc, Label, BookAId, BookBId, BookAMarginPercent, BookBMarginPercent, Note)
                    VALUES ('2026-08-21', 'Aug brief', 1, 2, '14.5', '14.5', 'Download PDF');
                    INSERT INTO VictoryReportSnapshotLines (
                        SnapshotId, PriceBookId, FormulationId, BookRole, FormulationCode, SellMt, SellBag, CurrencyCode)
                    VALUES (1, 1, NULL, 'A', 'OLD-CODE', '100', '4', 'KES');
                    """;
                cmd.ExecuteNonQuery();
            }

            using (var db = new CostWiseDbContext(options))
            {
                db.Database.Migrate();
                var line = db.VictoryReportSnapshotLines.Single();
                Assert.Equal("Pre-grower", line.FeedTypeName);
                Assert.Equal("2mm", line.SizeName);
                Assert.Null(line.FormulationId);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
