using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostWise.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceBookSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "PriceBooks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Assign stable order by name for existing books (SQLite).
            migrationBuilder.Sql("""
                UPDATE PriceBooks
                SET SortOrder = (
                    SELECT COUNT(*) - 1
                    FROM PriceBooks AS b2
                    WHERE b2.Name < PriceBooks.Name
                       OR (b2.Name = PriceBooks.Name AND b2.Id <= PriceBooks.Id)
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "PriceBooks");
        }
    }
}
