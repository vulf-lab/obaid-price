using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostWise.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceBookRoundingAndSellOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RoundBagTo",
                table: "PriceBooks",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RoundMtTo",
                table: "PriceBooks",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OverrideSellPriceBag",
                table: "PriceBookFormulations",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverrideSellPriceMt",
                table: "PriceBookFormulations",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoundBagTo",
                table: "PriceBooks");

            migrationBuilder.DropColumn(
                name: "RoundMtTo",
                table: "PriceBooks");

            migrationBuilder.DropColumn(
                name: "OverrideSellPriceBag",
                table: "PriceBookFormulations");

            migrationBuilder.DropColumn(
                name: "OverrideSellPriceMt",
                table: "PriceBookFormulations");
        }
    }
}
