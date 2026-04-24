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

            migrationBuilder.Sql(
                """
                ALTER TABLE "MetalReceiptItems"
                    ADD COLUMN IF NOT EXISTS "ConsumedAt" timestamp with time zone;

                ALTER TABLE "MetalReceiptItems"
                    ADD COLUMN IF NOT EXISTS "ConsumedByCuttingReportId" uuid;

                ALTER TABLE "MetalReceiptItems"
                    ADD COLUMN IF NOT EXISTS "IsConsumed" boolean NOT NULL DEFAULT FALSE;

                ALTER TABLE "MetalReceiptItems"
                    ADD COLUMN IF NOT EXISTS "SourceCuttingReportId" uuid;
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "CuttingReports" (
                    "Id" uuid NOT NULL,
                    "ReportNumber" text NOT NULL,
                    "ReportDate" timestamp with time zone NOT NULL,
                    "CuttingPlanId" uuid NOT NULL,
                    "SourceMetalReceiptItemId" uuid NOT NULL,
                    "Workshop" text NOT NULL,
                    "Shift" text NOT NULL,
                    "PlannedSize" numeric NOT NULL,
                    "ActualProducedSize" numeric NOT NULL,
                    "PlannedMassKg" numeric NOT NULL,
                    "ActualProducedMassKg" numeric NOT NULL,
                    "PlannedWaste" numeric NOT NULL,
                    "ActualWaste" numeric NOT NULL,
                    "BusinessResidual" numeric NOT NULL,
                    "ScrapSize" numeric NOT NULL,
                    "ScrapMassKg" numeric NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_CuttingReports" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_CuttingReports_MetalReceiptItems_SourceMetalReceiptItemId"
                        FOREIGN KEY ("SourceMetalReceiptItemId")
                        REFERENCES "MetalReceiptItems" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_CuttingReports_cutting_plan_CuttingPlanId"
                        FOREIGN KEY ("CuttingPlanId")
                        REFERENCES "cutting_plan" ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_MetalRequirements_MetalMaterialId",
                table: "MetalRequirements",
                column: "MetalMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MetalRequirements_WipLaunchId",
                table: "MetalRequirements",
                column: "WipLaunchId",
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_CuttingReports_CuttingPlanId"
                    ON "CuttingReports" ("CuttingPlanId");

                CREATE INDEX IF NOT EXISTS "IX_CuttingReports_SourceMetalReceiptItemId"
                    ON "CuttingReports" ("SourceMetalReceiptItemId");
                """);

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
