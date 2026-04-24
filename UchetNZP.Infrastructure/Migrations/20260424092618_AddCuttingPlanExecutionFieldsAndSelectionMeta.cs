using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCuttingPlanExecutionFieldsAndSelectionMeta : Migration
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

            migrationBuilder.AddColumn<decimal>(
                name: "ActualResidual",
                table: "cutting_plan",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutionStatus",
                table: "cutting_plan",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CandidateMaterials",
                table: "MetalRequirementItems");

            migrationBuilder.DropColumn(
                name: "SelectionReason",
                table: "MetalRequirementItems");

            migrationBuilder.DropColumn(
                name: "SelectionSource",
                table: "MetalRequirementItems");

            migrationBuilder.DropColumn(
                name: "ActualResidual",
                table: "cutting_plan");

            migrationBuilder.DropColumn(
                name: "ExecutionStatus",
                table: "cutting_plan");
        }
    }
}
