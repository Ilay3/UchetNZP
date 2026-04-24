using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddElectronicMetalRequirementsDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetalRequirements_WipLaunchId",
                table: "MetalRequirements");

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "MetalRequirements",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "MetalRequirements",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "MetalMaterialId",
                table: "MetalRequirements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "PartCode",
                table: "MetalRequirements",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PartName",
                table: "MetalRequirements",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "MetalRequirements",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "WipReceiptId",
                table: "MetalRequirements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "MetalRequirementItems",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConsumptionPerUnit",
                table: "MetalRequirementItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ConsumptionUnit",
                table: "MetalRequirementItems",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "RequiredQty",
                table: "MetalRequirementItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RequiredWeightKg",
                table: "MetalRequirementItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SizeRaw",
                table: "MetalRequirementItems",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsumedAt",
                table: "MetalReceiptItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConsumedByCuttingReportId",
                table: "MetalReceiptItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConsumed",
                table: "MetalReceiptItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceCuttingReportId",
                table: "MetalReceiptItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CuttingReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportNumber = table.Column<string>(type: "text", nullable: false),
                    ReportDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CuttingPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceMetalReceiptItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Workshop = table.Column<string>(type: "text", nullable: false),
                    Shift = table.Column<string>(type: "text", nullable: false),
                    PlannedSize = table.Column<decimal>(type: "numeric", nullable: false),
                    ActualProducedSize = table.Column<decimal>(type: "numeric", nullable: false),
                    PlannedMassKg = table.Column<decimal>(type: "numeric", nullable: false),
                    ActualProducedMassKg = table.Column<decimal>(type: "numeric", nullable: false),
                    PlannedWaste = table.Column<decimal>(type: "numeric", nullable: false),
                    ActualWaste = table.Column<decimal>(type: "numeric", nullable: false),
                    BusinessResidual = table.Column<decimal>(type: "numeric", nullable: false),
                    ScrapSize = table.Column<decimal>(type: "numeric", nullable: false),
                    ScrapMassKg = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuttingReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CuttingReports_MetalReceiptItems_SourceMetalReceiptItemId",
                        column: x => x.SourceMetalReceiptItemId,
                        principalTable: "MetalReceiptItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CuttingReports_cutting_plan_CuttingPlanId",
                        column: x => x.CuttingPlanId,
                        principalTable: "cutting_plan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetalRequirements_MetalMaterialId",
                table: "MetalRequirements",
                column: "MetalMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MetalRequirements_WipLaunchId",
                table: "MetalRequirements",
                column: "WipLaunchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CuttingReports_CuttingPlanId",
                table: "CuttingReports",
                column: "CuttingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CuttingReports_SourceMetalReceiptItemId",
                table: "CuttingReports",
                column: "SourceMetalReceiptItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_MetalRequirements_MetalMaterials_MetalMaterialId",
                table: "MetalRequirements",
                column: "MetalMaterialId",
                principalTable: "MetalMaterials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetalRequirements_MetalMaterials_MetalMaterialId",
                table: "MetalRequirements");

            migrationBuilder.DropTable(
                name: "CuttingReports");

            migrationBuilder.DropIndex(
                name: "IX_MetalRequirements_MetalMaterialId",
                table: "MetalRequirements");

            migrationBuilder.DropIndex(
                name: "IX_MetalRequirements_WipLaunchId",
                table: "MetalRequirements");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "MetalRequirements");

            migrationBuilder.DropColumn(
                name: "MetalMaterialId",
                table: "MetalRequirements");

            migrationBuilder.DropColumn(
                name: "PartCode",
                table: "MetalRequirements");

            migrationBuilder.DropColumn(
                name: "PartName",
                table: "MetalRequirements");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "MetalRequirements");

            migrationBuilder.DropColumn(
                name: "WipReceiptId",
                table: "MetalRequirements");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "MetalRequirementItems");

            migrationBuilder.DropColumn(
                name: "ConsumptionPerUnit",
                table: "MetalRequirementItems");

            migrationBuilder.DropColumn(
                name: "ConsumptionUnit",
                table: "MetalRequirementItems");

            migrationBuilder.DropColumn(
                name: "RequiredQty",
                table: "MetalRequirementItems");

            migrationBuilder.DropColumn(
                name: "RequiredWeightKg",
                table: "MetalRequirementItems");

            migrationBuilder.DropColumn(
                name: "SizeRaw",
                table: "MetalRequirementItems");

            migrationBuilder.DropColumn(
                name: "ConsumedAt",
                table: "MetalReceiptItems");

            migrationBuilder.DropColumn(
                name: "ConsumedByCuttingReportId",
                table: "MetalReceiptItems");

            migrationBuilder.DropColumn(
                name: "IsConsumed",
                table: "MetalReceiptItems");

            migrationBuilder.DropColumn(
                name: "SourceCuttingReportId",
                table: "MetalReceiptItems");

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "MetalRequirements",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetalRequirements_WipLaunchId",
                table: "MetalRequirements",
                column: "WipLaunchId");
        }
    }
}
