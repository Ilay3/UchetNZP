using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLabelYearToWipLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WipLabels_Number",
                table: "WipLabels");

            migrationBuilder.AddColumn<int>(
                name: "LabelYear",
                table: "WipLabels",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "WipLabels"
                SET "LabelYear" = EXTRACT(YEAR FROM "LabelDate")::integer
                WHERE "LabelYear" = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WipLabels_Number_LabelYear",
                table: "WipLabels",
                columns: new[] { "Number", "LabelYear" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WipLabels_Number_LabelYear",
                table: "WipLabels");

            migrationBuilder.DropColumn(
                name: "LabelYear",
                table: "WipLabels");

            migrationBuilder.CreateIndex(
                name: "IX_WipLabels_Number",
                table: "WipLabels",
                column: "Number",
                unique: true);
        }
    }
}
