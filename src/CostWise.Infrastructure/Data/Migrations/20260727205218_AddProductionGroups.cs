using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostWise.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductionGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionGroupFormulations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductionGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    FormulationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionGroupFormulations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionGroupFormulations_Formulations_FormulationId",
                        column: x => x.FormulationId,
                        principalTable: "Formulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionGroupFormulations_ProductionGroups_ProductionGroupId",
                        column: x => x.ProductionGroupId,
                        principalTable: "ProductionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionGroupFormulations_FormulationId",
                table: "ProductionGroupFormulations",
                column: "FormulationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionGroupFormulations_ProductionGroupId_FormulationId",
                table: "ProductionGroupFormulations",
                columns: new[] { "ProductionGroupId", "FormulationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionGroups_Name",
                table: "ProductionGroups",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionGroupFormulations");

            migrationBuilder.DropTable(
                name: "ProductionGroups");
        }
    }
}
