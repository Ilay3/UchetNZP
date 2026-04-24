using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMetalRequirementPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetalRequirementPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MetalRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RequiredQty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    PlannedQty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    DeficitQty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    CalculationComment = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RecalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecalculatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetalRequirementPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetalRequirementPlans_MetalRequirements_MetalRequirementId",
                        column: x => x.MetalRequirementId,
                        principalTable: "MetalRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetalRequirementPlanItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MetalRequirementPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetalReceiptItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceSize = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    SourceUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceWeightKg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    PlannedUseQty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    RemainingAfterQty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    LineStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetalRequirementPlanItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetalRequirementPlanItems_MetalReceiptItems_MetalReceiptIte~",
                        column: x => x.MetalReceiptItemId,
                        principalTable: "MetalReceiptItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MetalRequirementPlanItems_MetalRequirementPlans_MetalRequir~",
                        column: x => x.MetalRequirementPlanId,
                        principalTable: "MetalRequirementPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetalRequirementPlanItems_MetalReceiptItemId",
                table: "MetalRequirementPlanItems",
                column: "MetalReceiptItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MetalRequirementPlanItems_MetalRequirementPlanId_SortOrder",
                table: "MetalRequirementPlanItems",
                columns: new[] { "MetalRequirementPlanId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MetalRequirementPlans_MetalRequirementId",
                table: "MetalRequirementPlans",
                column: "MetalRequirementId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetalRequirementPlanItems");

            migrationBuilder.DropTable(
                name: "MetalRequirementPlans");
        }
    }
}
