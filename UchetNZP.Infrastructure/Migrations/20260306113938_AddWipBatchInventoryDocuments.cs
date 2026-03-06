using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWipBatchInventoryDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WipBatchInventoryDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryNumber = table.Column<int>(type: "integer", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ComposedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PartFilter = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SectionFilter = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OpNumberFilter = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    TotalQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipBatchInventoryDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WipBatchInventoryDocuments_GeneratedAt",
                table: "WipBatchInventoryDocuments",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WipBatchInventoryDocuments_InventoryNumber",
                table: "WipBatchInventoryDocuments",
                column: "InventoryNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WipBatchInventoryDocuments");
        }
    }
}
