using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancialIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeCnpjIndexNonUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_companies_cnpj",
                table: "companies");

            migrationBuilder.CreateIndex(
                name: "ix_companies_cnpj",
                table: "companies",
                column: "cnpj");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_companies_cnpj",
                table: "companies");

            migrationBuilder.CreateIndex(
                name: "ix_companies_cnpj",
                table: "companies",
                column: "cnpj",
                unique: true);
        }
    }
}
