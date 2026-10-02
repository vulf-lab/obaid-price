using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostWise.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVictorySnapshotProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FeedTypeName",
                table: "VictoryReportSnapshotLines",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SizeName",
                table: "VictoryReportSnapshotLines",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE VictoryReportSnapshotLines
                SET FeedTypeName = COALESCE((
                        SELECT ft.Name
                        FROM Formulations AS f
                        INNER JOIN FeedTypes AS ft ON ft.Id = f.FeedTypeId
                        WHERE f.Id = VictoryReportSnapshotLines.FormulationId
                    ), ''),
                    SizeName = COALESCE((
                        SELECT sz.Name
                        FROM Formulations AS f
                        INNER JOIN Sizes AS sz ON sz.Id = f.SizeId
                        WHERE f.Id = VictoryReportSnapshotLines.FormulationId
                    ), '')
                WHERE FormulationId IS NOT NULL
                  AND (FeedTypeName IS NULL OR FeedTypeName = '' OR SizeName IS NULL OR SizeName = '');

                UPDATE VictoryReportSnapshotLines
                SET FeedTypeName = CASE
                        WHEN FeedTypeName IS NULL OR FeedTypeName = '' THEN COALESCE((
                            SELECT ft.Name
                            FROM Formulations AS f
                            INNER JOIN FeedTypes AS ft ON ft.Id = f.FeedTypeId
                            WHERE f.Code = VictoryReportSnapshotLines.FormulationCode
                            LIMIT 1
                        ), '')
                        ELSE FeedTypeName
                    END,
                    SizeName = CASE
                        WHEN SizeName IS NULL OR SizeName = '' THEN COALESCE((
                            SELECT sz.Name
                            FROM Formulations AS f
                            INNER JOIN Sizes AS sz ON sz.Id = f.SizeId
                            WHERE f.Code = VictoryReportSnapshotLines.FormulationCode
                            LIMIT 1
                        ), '')
                        ELSE SizeName
                    END
                WHERE (FeedTypeName IS NULL OR FeedTypeName = '' OR SizeName IS NULL OR SizeName = '')
                  AND FormulationCode IS NOT NULL
                  AND FormulationCode <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeedTypeName",
                table: "VictoryReportSnapshotLines");

            migrationBuilder.DropColumn(
                name: "SizeName",
                table: "VictoryReportSnapshotLines");
        }
    }
}
