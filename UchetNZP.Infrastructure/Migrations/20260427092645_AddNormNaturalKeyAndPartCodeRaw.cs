using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNormNaturalKeyAndPartCodeRaw : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodeRaw",
                table: "Parts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsumptionTextRaw",
                table: "MetalConsumptionNorms",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormKeyHash",
                table: "MetalConsumptionNorms",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedConsumptionUnit",
                table: "MetalConsumptionNorms",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedSizeRaw",
                table: "MetalConsumptionNorms",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MetalConsumptionNorms_NormKeyHash",
                table: "MetalConsumptionNorms",
                column: "NormKeyHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetalConsumptionNorms_NormKeyHash",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "CodeRaw",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "ConsumptionTextRaw",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "NormKeyHash",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "NormalizedConsumptionUnit",
                table: "MetalConsumptionNorms");

            migrationBuilder.DropColumn(
                name: "NormalizedSizeRaw",
                table: "MetalConsumptionNorms");
        }
    }
}
