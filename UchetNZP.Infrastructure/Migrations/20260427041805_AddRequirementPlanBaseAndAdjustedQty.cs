using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequirementPlanBaseAndAdjustedQty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RequiredQty",
                table: "MetalRequirementPlans",
                newName: "BaseRequiredQty");

            migrationBuilder.AddColumn<decimal>(
                name: "AdjustedRequiredQty",
                table: "MetalRequirementPlans",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdjustedRequiredQty",
                table: "MetalRequirementPlans");

            migrationBuilder.RenameColumn(
                name: "BaseRequiredQty",
                table: "MetalRequirementPlans",
                newName: "RequiredQty");
        }
    }
}
