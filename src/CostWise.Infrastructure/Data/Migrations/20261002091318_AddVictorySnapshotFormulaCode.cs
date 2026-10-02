using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostWise.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVictorySnapshotFormulaCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VictoryReportSnapshotLines_Formulations_FormulationId",
                table: "VictoryReportSnapshotLines");

            migrationBuilder.DropForeignKey(
                name: "FK_VictoryReportSnapshotLines_PriceBooks_PriceBookId",
                table: "VictoryReportSnapshotLines");

            migrationBuilder.AddColumn<string>(
                name: "BriefJson",
                table: "VictoryReportSnapshots",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PriceBookId",
                table: "VictoryReportSnapshotLines",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "FormulationId",
                table: "VictoryReportSnapshotLines",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "BookRole",
                table: "VictoryReportSnapshotLines",
                type: "TEXT",
                maxLength: 1,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FormulationCode",
                table: "VictoryReportSnapshotLines",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE VictoryReportSnapshotLines
                SET FormulationCode = COALESCE((
                    SELECT f.Code FROM Formulations AS f
                    WHERE f.Id = VictoryReportSnapshotLines.FormulationId
                ), '')
                WHERE FormulationCode IS NULL OR FormulationCode = '';

                UPDATE VictoryReportSnapshotLines
                SET BookRole = COALESCE((
                    SELECT CASE
                        WHEN VictoryReportSnapshotLines.PriceBookId = s.BookAId THEN 'A'
                        ELSE 'B'
                    END
                    FROM VictoryReportSnapshots AS s
                    WHERE s.Id = VictoryReportSnapshotLines.SnapshotId
                ), 'B')
                WHERE BookRole IS NULL OR BookRole = '';
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_VictoryReportSnapshotLines_Formulations_FormulationId",
                table: "VictoryReportSnapshotLines",
                column: "FormulationId",
                principalTable: "Formulations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_VictoryReportSnapshotLines_PriceBooks_PriceBookId",
                table: "VictoryReportSnapshotLines",
                column: "PriceBookId",
                principalTable: "PriceBooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VictoryReportSnapshotLines_Formulations_FormulationId",
                table: "VictoryReportSnapshotLines");

            migrationBuilder.DropForeignKey(
                name: "FK_VictoryReportSnapshotLines_PriceBooks_PriceBookId",
                table: "VictoryReportSnapshotLines");

            migrationBuilder.DropColumn(
                name: "BriefJson",
                table: "VictoryReportSnapshots");

            migrationBuilder.DropColumn(
                name: "BookRole",
                table: "VictoryReportSnapshotLines");

            migrationBuilder.DropColumn(
                name: "FormulationCode",
                table: "VictoryReportSnapshotLines");

            migrationBuilder.AlterColumn<int>(
                name: "PriceBookId",
                table: "VictoryReportSnapshotLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "FormulationId",
                table: "VictoryReportSnapshotLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_VictoryReportSnapshotLines_Formulations_FormulationId",
                table: "VictoryReportSnapshotLines",
                column: "FormulationId",
                principalTable: "Formulations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VictoryReportSnapshotLines_PriceBooks_PriceBookId",
                table: "VictoryReportSnapshotLines",
                column: "PriceBookId",
                principalTable: "PriceBooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
