using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCuttingPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectionReason",
                table: "MetalRequirementItems");

            migrationBuilder.DropColumn(
                name: "SelectionSource",
                table: "MetalRequirementItems");

            migrationBuilder.CreateTable(
                name: "cutting_plan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MetalRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ParametersJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    UtilizationPercent = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    WastePercent = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    CutCount = table.Column<int>(type: "integer", nullable: false),
                    BusinessResidual = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    ScrapResidual = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cutting_plan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cutting_plan_MetalRequirements_MetalRequirementId",
                        column: x => x.MetalRequirementId,
                        principalTable: "MetalRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cutting_plan_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CuttingPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockIndex = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Length = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    Width = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    Height = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    PositionX = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    PositionY = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    Rotated = table.Column<bool>(type: "boolean", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cutting_plan_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cutting_plan_items_cutting_plan_CuttingPlanId",
                        column: x => x.CuttingPlanId,
                        principalTable: "cutting_plan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cutting_plan_MetalRequirementId_Kind_IsCurrent",
                table: "cutting_plan",
                columns: new[] { "MetalRequirementId", "Kind", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_cutting_plan_MetalRequirementId_Kind_Version",
                table: "cutting_plan",
                columns: new[] { "MetalRequirementId", "Kind", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cutting_plan_items_CuttingPlanId_StockIndex_Sequence",
                table: "cutting_plan_items",
                columns: new[] { "CuttingPlanId", "StockIndex", "Sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cutting_plan_items");

            migrationBuilder.DropTable(
                name: "cutting_plan");

            migrationBuilder.AddColumn<string>(
                name: "SelectionReason",
                table: "MetalRequirementItems",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectionSource",
                table: "MetalRequirementItems",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }
    }
}
