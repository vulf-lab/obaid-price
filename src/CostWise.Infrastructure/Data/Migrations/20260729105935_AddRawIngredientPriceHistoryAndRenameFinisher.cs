using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostWise.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRawIngredientPriceHistoryAndRenameFinisher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RawIngredientPriceHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RawIngredientId = table.Column<int>(type: "INTEGER", nullable: false),
                    PricePerMt = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ExchangeRateKesPerUsd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    PricePerMtUsd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawIngredientPriceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawIngredientPriceHistories_RawIngredients_RawIngredientId",
                        column: x => x.RawIngredientId,
                        principalTable: "RawIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RawIngredientPriceHistories_RawIngredientId_ChangedAtUtc",
                table: "RawIngredientPriceHistories",
                columns: new[] { "RawIngredientId", "ChangedAtUtc" });

            migrationBuilder.Sql("""
                UPDATE FeedTypes
                SET Name = 'Balanced Finisher'
                WHERE Name = 'Finisher Low Pro';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE FeedTypes
                SET Name = 'Finisher Low Pro'
                WHERE Name = 'Balanced Finisher';
                """);

            migrationBuilder.DropTable(
                name: "RawIngredientPriceHistories");
        }
    }
}
