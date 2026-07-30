using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostWise.Infrastructure.Data.Migrations;

[DbContext(typeof(CostWiseDbContext))]
[Migration("20260729180000_AddCommercialPriceLists")]
public partial class AddCommercialPriceLists : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CommercialPriceLists",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                CurrencyId = table.Column<int>(type: "INTEGER", nullable: true),
                SellUnit = table.Column<int>(type: "INTEGER", nullable: false),
                EffectiveDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CommercialPriceLists", x => x.Id);
                table.ForeignKey(
                    name: "FK_CommercialPriceLists_Currencies_CurrencyId",
                    column: x => x.CurrencyId,
                    principalTable: "Currencies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "CommercialPriceListBooks",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CommercialPriceListId = table.Column<int>(type: "INTEGER", nullable: false),
                PriceBookId = table.Column<int>(type: "INTEGER", nullable: false),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CommercialPriceListBooks", x => x.Id);
                table.ForeignKey(
                    name: "FK_CommercialPriceListBooks_CommercialPriceLists_CommercialPriceListId",
                    column: x => x.CommercialPriceListId,
                    principalTable: "CommercialPriceLists",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_CommercialPriceListBooks_PriceBooks_PriceBookId",
                    column: x => x.PriceBookId,
                    principalTable: "PriceBooks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CommercialPriceLists_CurrencyId",
            table: "CommercialPriceLists",
            column: "CurrencyId");

        migrationBuilder.CreateIndex(
            name: "IX_CommercialPriceLists_Name",
            table: "CommercialPriceLists",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CommercialPriceListBooks_CommercialPriceListId_PriceBookId",
            table: "CommercialPriceListBooks",
            columns: new[] { "CommercialPriceListId", "PriceBookId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CommercialPriceListBooks_PriceBookId",
            table: "CommercialPriceListBooks",
            column: "PriceBookId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CommercialPriceListBooks");
        migrationBuilder.DropTable(name: "CommercialPriceLists");
    }
}
