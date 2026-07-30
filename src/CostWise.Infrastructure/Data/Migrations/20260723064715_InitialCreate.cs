using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostWise.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeedTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RawIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    PricePerMt = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    IsAvailable = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawIngredients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Species",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Species", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpecParameters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sizes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DiameterMm = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    ConversionCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    FeedTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sizes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sizes_FeedTypes_FeedTypeId",
                        column: x => x.FeedTypeId,
                        principalTable: "FeedTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Formulations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    FeedTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    SpeciesId = table.Column<int>(type: "INTEGER", nullable: false),
                    SizeId = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Formulations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Formulations_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Formulations_FeedTypes_FeedTypeId",
                        column: x => x.FeedTypeId,
                        principalTable: "FeedTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Formulations_Sizes_SizeId",
                        column: x => x.SizeId,
                        principalTable: "Sizes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Formulations_Species_SpeciesId",
                        column: x => x.SpeciesId,
                        principalTable: "Species",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostingScenarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FormulationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Market = table.Column<int>(type: "INTEGER", nullable: false),
                    PackingCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ExportDocCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SpecialAdditiveCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TransportationCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MarginPercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    SnapshotRmCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SnapshotConversionCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SnapshotTotalCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SnapshotSellingPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostingScenarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostingScenarios_Formulations_FormulationId",
                        column: x => x.FormulationId,
                        principalTable: "Formulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FormulationIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FormulationId = table.Column<int>(type: "INTEGER", nullable: false),
                    RawIngredientId = table.Column<int>(type: "INTEGER", nullable: false),
                    InclusionPercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormulationIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormulationIngredients_Formulations_FormulationId",
                        column: x => x.FormulationId,
                        principalTable: "Formulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FormulationIngredients_RawIngredients_RawIngredientId",
                        column: x => x.RawIngredientId,
                        principalTable: "RawIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormulationSpecs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FormulationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SpecParameterId = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetValue = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: true),
                    MinValue = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: true),
                    MaxValue = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormulationSpecs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormulationSpecs_Formulations_FormulationId",
                        column: x => x.FormulationId,
                        principalTable: "Formulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FormulationSpecs_SpecParameters_SpecParameterId",
                        column: x => x.SpecParameterId,
                        principalTable: "SpecParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostingScenarios_FormulationId",
                table: "CostingScenarios",
                column: "FormulationId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedTypes_Name",
                table: "FeedTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormulationIngredients_FormulationId_RawIngredientId",
                table: "FormulationIngredients",
                columns: new[] { "FormulationId", "RawIngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormulationIngredients_RawIngredientId",
                table: "FormulationIngredients",
                column: "RawIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_Formulations_CategoryId",
                table: "Formulations",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Formulations_Code",
                table: "Formulations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Formulations_FeedTypeId",
                table: "Formulations",
                column: "FeedTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Formulations_SizeId",
                table: "Formulations",
                column: "SizeId");

            migrationBuilder.CreateIndex(
                name: "IX_Formulations_SpeciesId",
                table: "Formulations",
                column: "SpeciesId");

            migrationBuilder.CreateIndex(
                name: "IX_FormulationSpecs_FormulationId_SpecParameterId",
                table: "FormulationSpecs",
                columns: new[] { "FormulationId", "SpecParameterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormulationSpecs_SpecParameterId",
                table: "FormulationSpecs",
                column: "SpecParameterId");

            migrationBuilder.CreateIndex(
                name: "IX_RawIngredients_Name",
                table: "RawIngredients",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sizes_FeedTypeId",
                table: "Sizes",
                column: "FeedTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Species_Name",
                table: "Species",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecParameters_Name",
                table: "SpecParameters",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CostingScenarios");

            migrationBuilder.DropTable(
                name: "FormulationIngredients");

            migrationBuilder.DropTable(
                name: "FormulationSpecs");

            migrationBuilder.DropTable(
                name: "RawIngredients");

            migrationBuilder.DropTable(
                name: "Formulations");

            migrationBuilder.DropTable(
                name: "SpecParameters");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Sizes");

            migrationBuilder.DropTable(
                name: "Species");

            migrationBuilder.DropTable(
                name: "FeedTypes");
        }
    }
}
