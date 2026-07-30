using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostWise.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingCostOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdditiveOptionId",
                table: "PriceBooks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExportDocOptionId",
                table: "PriceBooks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PackingOptionId",
                table: "PriceBooks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdditiveOptionId",
                table: "CostingScenarios",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExportDocOptionId",
                table: "CostingScenarios",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PackingOptionId",
                table: "CostingScenarios",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PricingCostOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Cost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingCostOptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceBooks_AdditiveOptionId",
                table: "PriceBooks",
                column: "AdditiveOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceBooks_ExportDocOptionId",
                table: "PriceBooks",
                column: "ExportDocOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceBooks_PackingOptionId",
                table: "PriceBooks",
                column: "PackingOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_CostingScenarios_AdditiveOptionId",
                table: "CostingScenarios",
                column: "AdditiveOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_CostingScenarios_ExportDocOptionId",
                table: "CostingScenarios",
                column: "ExportDocOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_CostingScenarios_PackingOptionId",
                table: "CostingScenarios",
                column: "PackingOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingCostOptions_Kind_Name",
                table: "PricingCostOptions",
                columns: new[] { "Kind", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CostingScenarios_PricingCostOptions_AdditiveOptionId",
                table: "CostingScenarios",
                column: "AdditiveOptionId",
                principalTable: "PricingCostOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CostingScenarios_PricingCostOptions_ExportDocOptionId",
                table: "CostingScenarios",
                column: "ExportDocOptionId",
                principalTable: "PricingCostOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CostingScenarios_PricingCostOptions_PackingOptionId",
                table: "CostingScenarios",
                column: "PackingOptionId",
                principalTable: "PricingCostOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceBooks_PricingCostOptions_AdditiveOptionId",
                table: "PriceBooks",
                column: "AdditiveOptionId",
                principalTable: "PricingCostOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceBooks_PricingCostOptions_ExportDocOptionId",
                table: "PriceBooks",
                column: "ExportDocOptionId",
                principalTable: "PricingCostOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceBooks_PricingCostOptions_PackingOptionId",
                table: "PriceBooks",
                column: "PackingOptionId",
                principalTable: "PricingCostOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CostingScenarios_PricingCostOptions_AdditiveOptionId",
                table: "CostingScenarios");

            migrationBuilder.DropForeignKey(
                name: "FK_CostingScenarios_PricingCostOptions_ExportDocOptionId",
                table: "CostingScenarios");

            migrationBuilder.DropForeignKey(
                name: "FK_CostingScenarios_PricingCostOptions_PackingOptionId",
                table: "CostingScenarios");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceBooks_PricingCostOptions_AdditiveOptionId",
                table: "PriceBooks");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceBooks_PricingCostOptions_ExportDocOptionId",
                table: "PriceBooks");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceBooks_PricingCostOptions_PackingOptionId",
                table: "PriceBooks");

            migrationBuilder.DropTable(
                name: "PricingCostOptions");

            migrationBuilder.DropIndex(
                name: "IX_PriceBooks_AdditiveOptionId",
                table: "PriceBooks");

            migrationBuilder.DropIndex(
                name: "IX_PriceBooks_ExportDocOptionId",
                table: "PriceBooks");

            migrationBuilder.DropIndex(
                name: "IX_PriceBooks_PackingOptionId",
                table: "PriceBooks");

            migrationBuilder.DropIndex(
                name: "IX_CostingScenarios_AdditiveOptionId",
                table: "CostingScenarios");

            migrationBuilder.DropIndex(
                name: "IX_CostingScenarios_ExportDocOptionId",
                table: "CostingScenarios");

            migrationBuilder.DropIndex(
                name: "IX_CostingScenarios_PackingOptionId",
                table: "CostingScenarios");

            migrationBuilder.DropColumn(
                name: "AdditiveOptionId",
                table: "PriceBooks");

            migrationBuilder.DropColumn(
                name: "ExportDocOptionId",
                table: "PriceBooks");

            migrationBuilder.DropColumn(
                name: "PackingOptionId",
                table: "PriceBooks");

            migrationBuilder.DropColumn(
                name: "AdditiveOptionId",
                table: "CostingScenarios");

            migrationBuilder.DropColumn(
                name: "ExportDocOptionId",
                table: "CostingScenarios");

            migrationBuilder.DropColumn(
                name: "PackingOptionId",
                table: "CostingScenarios");
        }
    }
}
