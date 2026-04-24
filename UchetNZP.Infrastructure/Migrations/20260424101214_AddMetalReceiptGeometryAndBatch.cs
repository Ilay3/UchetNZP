using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMetalReceiptGeometryAndBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "MetalReceipts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ActualWeightKg",
                table: "MetalReceiptItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CalculatedWeightKg",
                table: "MetalReceiptItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiameterMm",
                table: "MetalReceiptItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LengthMm",
                table: "MetalReceiptItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PassportWeightKg",
                table: "MetalReceiptItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ProfileType",
                table: "MetalReceiptItems",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StockCategory",
                table: "MetalReceiptItems",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ThicknessMm",
                table: "MetalReceiptItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WallThicknessMm",
                table: "MetalReceiptItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightDeviationKg",
                table: "MetalReceiptItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WidthMm",
                table: "MetalReceiptItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "MetalReceipts");

            migrationBuilder.DropColumn(
                name: "ActualWeightKg",
                table: "MetalReceiptItems");

            migrationBuilder.DropColumn(
                name: "CalculatedWeightKg",
                table: "MetalReceiptItems");

            migrationBuilder.DropColumn(
                name: "DiameterMm",
                table: "MetalReceiptItems");

            migrationBuilder.DropColumn(
                name: "LengthMm",
                table: "MetalReceiptItems");

            migrationBuilder.DropColumn(
                name: "PassportWeightKg",
                table: "MetalReceiptItems");

            migrationBuilder.DropColumn(
                name: "ProfileType",
                table: "MetalReceiptItems");

            migrationBuilder.DropColumn(
                name: "StockCategory",
                table: "MetalReceiptItems");

            migrationBuilder.DropColumn(
                name: "ThicknessMm",
                table: "MetalReceiptItems");

            migrationBuilder.DropColumn(
                name: "WallThicknessMm",
                table: "MetalReceiptItems");

            migrationBuilder.DropColumn(
                name: "WeightDeviationKg",
                table: "MetalReceiptItems");

            migrationBuilder.DropColumn(
                name: "WidthMm",
                table: "MetalReceiptItems");
        }
    }
}
