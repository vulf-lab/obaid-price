using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CostWise.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrenciesAndPriceBookDisplayCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    IsBase = table.Column<bool>(type: "INTEGER", nullable: false),
                    KesPerUnit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Code",
                table: "Currencies",
                column: "Code",
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: "DisplayCurrencyId",
                table: "PriceBooks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceBooks_DisplayCurrencyId",
                table: "PriceBooks",
                column: "DisplayCurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_PriceBooks_Currencies_DisplayCurrencyId",
                table: "PriceBooks",
                column: "DisplayCurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql("""
                INSERT INTO Currencies (Code, Name, IsBase, KesPerUnit, IsActive, SortOrder)
                SELECT 'KES','Kenyan Shilling',1,1,1,0 WHERE NOT EXISTS (SELECT 1 FROM Currencies WHERE Code='KES');
                INSERT INTO Currencies (Code, Name, IsBase, KesPerUnit, IsActive, SortOrder)
                SELECT 'USD','US Dollar',0,130,1,1 WHERE NOT EXISTS (SELECT 1 FROM Currencies WHERE Code='USD');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PriceBooks_Currencies_DisplayCurrencyId",
                table: "PriceBooks");

            migrationBuilder.DropIndex(
                name: "IX_PriceBooks_DisplayCurrencyId",
                table: "PriceBooks");

            migrationBuilder.DropColumn(
                name: "DisplayCurrencyId",
                table: "PriceBooks");

            migrationBuilder.DropTable(
                name: "Currencies");
        }
    }
}
