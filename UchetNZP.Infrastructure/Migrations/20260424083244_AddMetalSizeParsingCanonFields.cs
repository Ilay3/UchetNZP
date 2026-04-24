using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMetalSizeParsingCanonFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiameterMm",
                table: "MetalConsumptionNorms",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LengthMm",
                table: "MetalConsumptionNorms",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParseError",
                table: "MetalConsumptionNorms",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParseStatus",
                table: "MetalConsumptionNorms",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "failed");

            migrationBuilder.AddColumn<string>(
                name: "ShapeType",
                table: "MetalConsumptionNorms",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<decimal>(
                name: "ThicknessMm",
                table: "MetalConsumptionNorms",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitNorm",
                table: "MetalConsumptionNorms",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "pcs");

            migrationBuilder.AddColumn<decimal>(
                name: "ValueNorm",
                table: "MetalConsumptionNorms",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WidthMm",
                table: "MetalConsumptionNorms",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiameterMm",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "LengthMm",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "ParseError",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "ParseStatus",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "ShapeType",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "ThicknessMm",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "UnitNorm",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "ValueNorm",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "WidthMm",
                table: "MetalConsumptionNorms");
        }
    }
}
