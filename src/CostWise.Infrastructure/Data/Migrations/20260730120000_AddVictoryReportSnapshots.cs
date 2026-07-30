using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostWise.Infrastructure.Data.Migrations;

[DbContext(typeof(CostWiseDbContext))]
[Migration("20260730120000_AddVictoryReportSnapshots")]
public partial class AddVictoryReportSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VictoryReportSnapshots",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                BookAId = table.Column<int>(type: "INTEGER", nullable: false),
                BookBId = table.Column<int>(type: "INTEGER", nullable: false),
                BookAMarginPercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                BookBMarginPercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VictoryReportSnapshots", x => x.Id);
                table.ForeignKey(
                    name: "FK_VictoryReportSnapshots_PriceBooks_BookAId",
                    column: x => x.BookAId,
                    principalTable: "PriceBooks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VictoryReportSnapshots_PriceBooks_BookBId",
                    column: x => x.BookBId,
                    principalTable: "PriceBooks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "VictoryReportSnapshotLines",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                SnapshotId = table.Column<int>(type: "INTEGER", nullable: false),
                PriceBookId = table.Column<int>(type: "INTEGER", nullable: false),
                FormulationId = table.Column<int>(type: "INTEGER", nullable: false),
                SellMt = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                SellBag = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VictoryReportSnapshotLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_VictoryReportSnapshotLines_Formulations_FormulationId",
                    column: x => x.FormulationId,
                    principalTable: "Formulations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_VictoryReportSnapshotLines_PriceBooks_PriceBookId",
                    column: x => x.PriceBookId,
                    principalTable: "PriceBooks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_VictoryReportSnapshotLines_VictoryReportSnapshots_SnapshotId",
                    column: x => x.SnapshotId,
                    principalTable: "VictoryReportSnapshots",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_VictoryReportSnapshots_BookAId",
            table: "VictoryReportSnapshots",
            column: "BookAId");

        migrationBuilder.CreateIndex(
            name: "IX_VictoryReportSnapshots_BookBId",
            table: "VictoryReportSnapshots",
            column: "BookBId");

        migrationBuilder.CreateIndex(
            name: "IX_VictoryReportSnapshots_CreatedAtUtc",
            table: "VictoryReportSnapshots",
            column: "CreatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_VictoryReportSnapshotLines_FormulationId",
            table: "VictoryReportSnapshotLines",
            column: "FormulationId");

        migrationBuilder.CreateIndex(
            name: "IX_VictoryReportSnapshotLines_PriceBookId",
            table: "VictoryReportSnapshotLines",
            column: "PriceBookId");

        migrationBuilder.CreateIndex(
            name: "IX_VictoryReportSnapshotLines_SnapshotId_PriceBookId_FormulationId",
            table: "VictoryReportSnapshotLines",
            columns: new[] { "SnapshotId", "PriceBookId", "FormulationId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "VictoryReportSnapshotLines");
        migrationBuilder.DropTable(name: "VictoryReportSnapshots");
    }
}
