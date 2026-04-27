using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMetalReceiptItemActualBlankSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActualBlankSizeText",
                table: "MetalReceiptItems",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualBlankSizeText",
                table: "MetalReceiptItems");
        }
    }
}
