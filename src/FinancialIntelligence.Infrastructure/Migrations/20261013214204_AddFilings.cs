using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinancialIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFilings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_financial_facts_value_identity",
                table: "financial_facts");

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
                name: "filings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cvm_code = table.Column<int>(type: "integer", nullable: false),
                    form = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reference_date = table.Column<DateOnly>(type: "date", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    basis = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    layout = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_file = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    retrieved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_filings", x => x.id);
                    table.ForeignKey(
                        name: "fk_filings_companies_cvm_code",
                        column: x => x.cvm_code,
                        principalTable: "companies",
                        principalColumn: "cvm_code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_financial_facts_filing_id",
                table: "financial_facts",
                column: "filing_id");

            migrationBuilder.CreateIndex(
                name: "ux_financial_facts_value_identity",
                table: "financial_facts",
                columns: new[] { "cvm_code", "account_code", "basis", "period_start", "period_end", "version" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_filings_identity",
                table: "filings",
                columns: new[] { "cvm_code", "form", "reference_date", "version", "basis" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_financial_facts_filings_filing_id",
                table: "financial_facts",
                column: "filing_id",
                principalTable: "filings",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_financial_facts_filings_filing_id",
                table: "financial_facts");

            migrationBuilder.DropTable(
                name: "filings");

            migrationBuilder.DropIndex(
                name: "ix_financial_facts_filing_id",
                table: "financial_facts");

            migrationBuilder.DropIndex(
                name: "ux_financial_facts_value_identity",
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

            migrationBuilder.CreateIndex(
                name: "ux_financial_facts_value_identity",
                table: "financial_facts",
                columns: new[] { "cvm_code", "account_code", "basis", "period_start", "period_end", "version" },
                unique: true);
        }
    }
}
