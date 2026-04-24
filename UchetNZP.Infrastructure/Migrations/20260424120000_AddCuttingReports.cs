using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    public partial class AddCuttingReports : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                        name: "FK_CuttingReports_cutting_plan_CuttingPlanId",
                        column: x => x.CuttingPlanId,
                        principalTable: "cutting_plan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CuttingReports_MetalReceiptItems_SourceMetalReceiptItemId",
                        column: x => x.SourceMetalReceiptItemId,
                        principalTable: "MetalReceiptItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CuttingReports_CuttingPlanId",
                table: "CuttingReports",
                column: "CuttingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CuttingReports_ReportNumber",
                table: "CuttingReports",
                column: "ReportNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CuttingReports_SourceMetalReceiptItemId",
                table: "CuttingReports",
                column: "SourceMetalReceiptItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MetalReceiptItems_IsConsumed",
                table: "MetalReceiptItems",
                column: "IsConsumed");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CuttingReports");

            migrationBuilder.DropIndex(
                name: "IX_MetalReceiptItems_IsConsumed",
                table: "MetalReceiptItems");

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
        }
    }
}
