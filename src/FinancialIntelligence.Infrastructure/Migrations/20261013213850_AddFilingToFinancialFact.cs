using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinancialIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFilingToFinancialFact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "period_start",
                table: "financial_facts",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<long>(
                name: "filing_id",
                table: "financial_facts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "filing",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cvm_code = table.Column<int>(type: "integer", nullable: false),
                    form = table.Column<int>(type: "integer", nullable: false),
                    reference_date = table.Column<DateOnly>(type: "date", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    basis = table.Column<int>(type: "integer", nullable: false),
                    layout = table.Column<int>(type: "integer", nullable: false),
                    source_file = table.Column<string>(type: "text", nullable: false),
                    retrieved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_filing", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_financial_facts_filing_id",
                table: "financial_facts",
                column: "filing_id");

            migrationBuilder.AddForeignKey(
                name: "fk_financial_facts_filing_filing_id",
                table: "financial_facts",
                column: "filing_id",
                principalTable: "filing",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_financial_facts_filing_filing_id",
                table: "financial_facts");

            migrationBuilder.DropTable(
                name: "filing");

            migrationBuilder.DropIndex(
                name: "ix_financial_facts_filing_id",
                table: "financial_facts");

            migrationBuilder.DropColumn(
                name: "filing_id",
                table: "financial_facts");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "period_start",
                table: "financial_facts",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}
