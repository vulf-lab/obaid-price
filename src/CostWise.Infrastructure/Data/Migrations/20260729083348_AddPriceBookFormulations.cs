using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostWise.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceBookFormulations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceBookFormulations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PriceBookId = table.Column<int>(type: "INTEGER", nullable: false),
                    FormulationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceBookFormulations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceBookFormulations_Formulations_FormulationId",
                        column: x => x.FormulationId,
                        principalTable: "Formulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PriceBookFormulations_PriceBooks_PriceBookId",
                        column: x => x.PriceBookId,
                        principalTable: "PriceBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceBookFormulations_FormulationId",
                table: "PriceBookFormulations",
                column: "FormulationId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceBookFormulations_PriceBookId_FormulationId",
                table: "PriceBookFormulations",
                columns: new[] { "PriceBookId", "FormulationId" },
                unique: true);

            // Existing books keep today's "all active" set; new books stay empty.
            migrationBuilder.Sql("""
                INSERT INTO PriceBookFormulations (PriceBookId, FormulationId, SortOrder)
                SELECT pb.Id, f.Id,
                       (SELECT COUNT(*) FROM Formulations f2
                        WHERE f2.IsActive = 1 AND f2.Code < f.Code)
                FROM PriceBooks pb
                CROSS JOIN Formulations f
                WHERE f.IsActive = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceBookFormulations");
        }
    }
}
