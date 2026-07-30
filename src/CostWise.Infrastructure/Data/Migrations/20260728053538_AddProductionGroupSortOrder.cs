using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostWise.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionGroupSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "ProductionGroups",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Assign stable order by name for existing groups (SQLite).
            migrationBuilder.Sql("""
                UPDATE ProductionGroups
                SET SortOrder = (
                    SELECT COUNT(*) - 1
                    FROM ProductionGroups AS g2
                    WHERE g2.Name < ProductionGroups.Name
                       OR (g2.Name = ProductionGroups.Name AND g2.Id <= ProductionGroups.Id)
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "ProductionGroups");
        }
    }
}
