using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMetalSuppliersAndReceiptFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountWithoutVat",
                table: "MetalReceipts",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "MetalSupplierId",
                table: "MetalReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerKg",
                table: "MetalReceipts",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SupplierDocumentNumber",
                table: "MetalReceipts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierIdentifierSnapshot",
                table: "MetalReceipts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierInnSnapshot",
                table: "MetalReceipts",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierNameSnapshot",
                table: "MetalReceipts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmountWithVat",
                table: "MetalReceipts",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "MetalReceipts",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRatePercent",
                table: "MetalReceipts",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "MetalSuppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Identifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Inn = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetalSuppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemParameters",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DecimalValue = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    TextValue = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemParameters", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetalReceipts_MetalSupplierId",
                table: "MetalReceipts",
                column: "MetalSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_MetalSuppliers_Identifier",
                table: "MetalSuppliers",
                column: "Identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetalSuppliers_Inn",
                table: "MetalSuppliers",
                column: "Inn");

            migrationBuilder.AddForeignKey(
                name: "FK_MetalReceipts_MetalSuppliers_MetalSupplierId",
                table: "MetalReceipts",
                column: "MetalSupplierId",
                principalTable: "MetalSuppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetalReceipts_MetalSuppliers_MetalSupplierId",
                table: "MetalReceipts");

            migrationBuilder.DropTable(
                name: "MetalSuppliers");

            migrationBuilder.DropTable(
                name: "SystemParameters");

            migrationBuilder.DropIndex(
                name: "IX_MetalReceipts_MetalSupplierId",
                table: "MetalReceipts");

            migrationBuilder.DropColumn(
                name: "AmountWithoutVat",
                table: "MetalReceipts");

            migrationBuilder.DropColumn(
                name: "MetalSupplierId",
                table: "MetalReceipts");

            migrationBuilder.DropColumn(
                name: "PricePerKg",
                table: "MetalReceipts");

            migrationBuilder.DropColumn(
                name: "SupplierDocumentNumber",
                table: "MetalReceipts");

            migrationBuilder.DropColumn(
                name: "SupplierIdentifierSnapshot",
                table: "MetalReceipts");

            migrationBuilder.DropColumn(
                name: "SupplierInnSnapshot",
                table: "MetalReceipts");

            migrationBuilder.DropColumn(
                name: "SupplierNameSnapshot",
                table: "MetalReceipts");

            migrationBuilder.DropColumn(
                name: "TotalAmountWithVat",
                table: "MetalReceipts");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "MetalReceipts");

            migrationBuilder.DropColumn(
                name: "VatRatePercent",
                table: "MetalReceipts");
        }
    }
}
