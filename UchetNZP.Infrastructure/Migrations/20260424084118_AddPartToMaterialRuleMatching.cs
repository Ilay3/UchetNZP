using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartToMaterialRuleMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CandidateMaterials",
                table: "MetalRequirementItems",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

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

            migrationBuilder.CreateTable(
                name: "part_to_material_rule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartNamePattern = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    GeometryType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RolledType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SizeFromMm = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    SizeToMm = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    MaterialGradePattern = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MaterialArticle = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_part_to_material_rule", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_part_to_material_rule_IsActive_Priority",
                table: "part_to_material_rule",
                columns: new[] { "IsActive", "Priority" });

            migrationBuilder.InsertData(
                table: "part_to_material_rule",
                columns: new[]
                {
                    "Id", "PartNamePattern", "GeometryType", "RolledType", "SizeFromMm", "SizeToMm", "MaterialGradePattern", "MaterialArticle", "Priority", "IsActive",
                },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111001"), "штыр", "rod", "rod", 7m, 12m, null, "06.1006", 100, true },
                    { new Guid("11111111-1111-1111-1111-111111111002"), "бирк", "sheet", "sheet", 0.5m, 12m, null, "06.2001", 90, true },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "part_to_material_rule");

            migrationBuilder.DropColumn(
                name: "CandidateMaterials",
                table: "MetalRequirementItems");

            migrationBuilder.DropColumn(
                name: "SelectionReason",
                table: "MetalRequirementItems");

            migrationBuilder.DropColumn(
                name: "SelectionSource",
                table: "MetalRequirementItems");
        }
    }
}
