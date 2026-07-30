using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostWise.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceBooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceBooks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    PackingCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TransportationCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SpecialAdditiveCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ExportDocCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MarginPercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    PriceUnit = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceBooks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceBooks_Name",
                table: "PriceBooks",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceBooks");
        }
    }
}
