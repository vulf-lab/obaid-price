using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostWise.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemIdAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Formulations",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ImportBatchId",
                table: "Formulations",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImportedAtUtc",
                table: "Formulations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemId",
                table: "Formulations",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Formulations",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql("""
                UPDATE Formulations
                SET SystemId = 'CW-' || printf('%06d', Id),
                    CreatedAtUtc = CASE WHEN CreatedAtUtc < '2000-01-01' THEN datetime('now') ELSE CreatedAtUtc END,
                    UpdatedAtUtc = CASE WHEN UpdatedAtUtc < '2000-01-01' THEN datetime('now') ELSE UpdatedAtUtc END,
                    ImportedAtUtc = COALESCE(ImportedAtUtc, datetime('now'));
                """);

            migrationBuilder.CreateTable(
                name: "FormulationChangeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FormulationId = table.Column<int>(type: "INTEGER", nullable: true),
                    SystemId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    ImportBatchId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormulationChangeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormulationChangeLogs_Formulations_FormulationId",
                        column: x => x.FormulationId,
                        principalTable: "Formulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Formulations_SystemId",
                table: "Formulations",
                column: "SystemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormulationChangeLogs_ChangedAtUtc",
                table: "FormulationChangeLogs",
                column: "ChangedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FormulationChangeLogs_FormulationId",
                table: "FormulationChangeLogs",
                column: "FormulationId");

            migrationBuilder.CreateIndex(
                name: "IX_FormulationChangeLogs_SystemId",
                table: "FormulationChangeLogs",
                column: "SystemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormulationChangeLogs");

            migrationBuilder.DropIndex(
                name: "IX_Formulations_SystemId",
                table: "Formulations");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Formulations");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "Formulations");

            migrationBuilder.DropColumn(
                name: "ImportedAtUtc",
                table: "Formulations");

            migrationBuilder.DropColumn(
                name: "SystemId",
                table: "Formulations");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Formulations");
        }
    }
}
