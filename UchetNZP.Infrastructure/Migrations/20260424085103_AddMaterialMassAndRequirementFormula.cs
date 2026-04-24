using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialMassAndRequirementFormula : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalculationFormula",
                table: "MetalRequirementItems",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalculationInput",
                table: "MetalRequirementItems",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CoefConsumption",
                table: "MetalMaterials",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "MassPerMeterKg",
                table: "MetalMaterials",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MassPerSquareMeterKg",
                table: "MetalMaterials",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StockUnit",
                table: "MetalMaterials",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalculationFormula",
                table: "MetalRequirementItems");

            migrationBuilder.DropColumn(
                name: "CalculationInput",
                table: "MetalRequirementItems");

            migrationBuilder.DropColumn(
                name: "CoefConsumption",
                table: "MetalMaterials");

            migrationBuilder.DropColumn(
                name: "MassPerMeterKg",
                table: "MetalMaterials");

            migrationBuilder.DropColumn(
                name: "MassPerSquareMeterKg",
                table: "MetalMaterials");

            migrationBuilder.DropColumn(
                name: "StockUnit",
                table: "MetalMaterials");
        }
    }
}
