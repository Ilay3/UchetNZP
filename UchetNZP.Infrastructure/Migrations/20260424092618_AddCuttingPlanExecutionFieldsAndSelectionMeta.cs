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
            migrationBuilder.Sql("""
                ALTER TABLE "MetalRequirementItems"
                ADD COLUMN IF NOT EXISTS "CandidateMaterials" character varying(2048);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "MetalRequirementItems"
                ADD COLUMN IF NOT EXISTS "SelectionReason" character varying(512);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "MetalRequirementItems"
                ADD COLUMN IF NOT EXISTS "SelectionSource" character varying(32) NOT NULL DEFAULT '';
                """);

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
            migrationBuilder.Sql("""ALTER TABLE "MetalRequirementItems" DROP COLUMN IF EXISTS "CandidateMaterials";""");
            migrationBuilder.Sql("""ALTER TABLE "MetalRequirementItems" DROP COLUMN IF EXISTS "SelectionReason";""");
            migrationBuilder.Sql("""ALTER TABLE "MetalRequirementItems" DROP COLUMN IF EXISTS "SelectionSource";""");

            migrationBuilder.DropColumn(
                name: "ActualResidual",
                table: "cutting_plan");

            migrationBuilder.DropColumn(
                name: "ExecutionStatus",
                table: "cutting_plan");
        }
    }
}
