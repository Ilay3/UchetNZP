using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMetalImportEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetalConsumptionNorms_PartId_MetalMaterialId_IsActive",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "ConsumptionQty",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "WeightPerUnitKg",
                table: "MetalConsumptionNorms");

            migrationBuilder.AddColumn<decimal>(
                name: "Coefficient",
                table: "MetalMaterials",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "MetalMaterials",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightPerUnitKg",
                table: "MetalMaterials",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "MetalMaterialId",
                table: "MetalConsumptionNorms",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<decimal>(
                name: "BaseConsumptionQty",
                table: "MetalConsumptionNorms",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SizeRaw",
                table: "MetalConsumptionNorms",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFile",
                table: "MetalConsumptionNorms",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetalConsumptionNorms_PartId_IsActive",
                table: "MetalConsumptionNorms",
                columns: new[] { "PartId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetalConsumptionNorms_PartId_IsActive",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "Coefficient",
                table: "MetalMaterials");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "MetalMaterials");

            migrationBuilder.DropColumn(
                name: "WeightPerUnitKg",
                table: "MetalMaterials");

            migrationBuilder.DropColumn(
                name: "BaseConsumptionQty",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "SizeRaw",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "SourceFile",
                table: "MetalConsumptionNorms");

            migrationBuilder.AlterColumn<Guid>(
                name: "MetalMaterialId",
                table: "MetalConsumptionNorms",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConsumptionQty",
                table: "MetalConsumptionNorms",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightPerUnitKg",
                table: "MetalConsumptionNorms",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetalConsumptionNorms_PartId_MetalMaterialId_IsActive",
                table: "MetalConsumptionNorms",
                columns: new[] { "PartId", "MetalMaterialId", "IsActive" });
        }
    }
}
