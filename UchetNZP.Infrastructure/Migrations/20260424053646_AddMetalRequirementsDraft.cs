using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMetalRequirementsDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetalConsumptionNorms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetalMaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumptionQty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    ConsumptionUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    WeightPerUnitKg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    Comment = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetalConsumptionNorms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetalConsumptionNorms_MetalMaterials_MetalMaterialId",
                        column: x => x.MetalMaterialId,
                        principalTable: "MetalMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MetalConsumptionNorms_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetalRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequirementDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WipLaunchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetalRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetalRequirements_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MetalRequirements_WipLaunches_WipLaunchId",
                        column: x => x.WipLaunchId,
                        principalTable: "WipLaunches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MetalRequirementItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MetalRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetalMaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    NormPerUnit = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    TotalRequiredQty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TotalRequiredWeightKg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetalRequirementItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetalRequirementItems_MetalMaterials_MetalMaterialId",
                        column: x => x.MetalMaterialId,
                        principalTable: "MetalMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MetalRequirementItems_MetalRequirements_MetalRequirementId",
                        column: x => x.MetalRequirementId,
                        principalTable: "MetalRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetalConsumptionNorms_MetalMaterialId",
                table: "MetalConsumptionNorms",
                column: "MetalMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MetalConsumptionNorms_PartId_MetalMaterialId_IsActive",
                table: "MetalConsumptionNorms",
                columns: new[] { "PartId", "MetalMaterialId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MetalRequirementItems_MetalMaterialId",
                table: "MetalRequirementItems",
                column: "MetalMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MetalRequirementItems_MetalRequirementId_MetalMaterialId",
                table: "MetalRequirementItems",
                columns: new[] { "MetalRequirementId", "MetalMaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetalRequirements_PartId",
                table: "MetalRequirements",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_MetalRequirements_RequirementDate",
                table: "MetalRequirements",
                column: "RequirementDate");

            migrationBuilder.CreateIndex(
                name: "IX_MetalRequirements_RequirementNumber",
                table: "MetalRequirements",
                column: "RequirementNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetalRequirements_WipLaunchId",
                table: "MetalRequirements",
                column: "WipLaunchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetalConsumptionNorms");

            migrationBuilder.DropTable(
                name: "MetalRequirementItems");

            migrationBuilder.DropTable(
                name: "MetalRequirements");
        }
    }
}
