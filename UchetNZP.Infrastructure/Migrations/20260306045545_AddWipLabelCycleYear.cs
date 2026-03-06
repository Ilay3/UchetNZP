using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWipLabelCycleYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WipLabels_Number",
                table: "WipLabels");

            migrationBuilder.DropIndex(
                name: "IX_WipLabels_RootNumber_Suffix",
                table: "WipLabels");

            migrationBuilder.AddColumn<int>(
                name: "CycleYear",
                table: "WipLabels",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE "WipLabels"
                SET "CycleYear" = EXTRACT(YEAR FROM "LabelDate")::integer
                WHERE "CycleYear" = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WipLabels_Number_CycleYear",
                table: "WipLabels",
                columns: new[] { "Number", "CycleYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WipLabels_RootNumber_Suffix_CycleYear",
                table: "WipLabels",
                columns: new[] { "RootNumber", "Suffix", "CycleYear" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WipLabels_Number_CycleYear",
                table: "WipLabels");

            migrationBuilder.DropIndex(
                name: "IX_WipLabels_RootNumber_Suffix_CycleYear",
                table: "WipLabels");

            migrationBuilder.DropColumn(
                name: "CycleYear",
                table: "WipLabels");

            migrationBuilder.CreateIndex(
                name: "IX_WipLabels_Number",
                table: "WipLabels",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WipLabels_RootNumber_Suffix",
                table: "WipLabels",
                columns: new[] { "RootNumber", "Suffix" },
                unique: true);
        }
    }
}
