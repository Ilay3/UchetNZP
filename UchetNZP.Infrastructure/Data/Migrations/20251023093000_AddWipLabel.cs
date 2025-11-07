using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWipLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WipLabels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    LabelDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Number = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    IsAssigned = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipLabels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WipLabels_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "WipLabelId",
                table: "WipReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WipLabels_Number",
                table: "WipLabels",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WipLabels_PartId",
                table: "WipLabels",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_WipReceipts_WipLabelId",
                table: "WipReceipts",
                column: "WipLabelId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WipReceipts_WipLabels_WipLabelId",
                table: "WipReceipts",
                column: "WipLabelId",
                principalTable: "WipLabels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WipReceipts_WipLabels_WipLabelId",
                table: "WipReceipts");

            migrationBuilder.DropTable(
                name: "WipLabels");

            migrationBuilder.DropIndex(
                name: "IX_WipReceipts_WipLabelId",
                table: "WipReceipts");

            migrationBuilder.DropColumn(
                name: "WipLabelId",
                table: "WipReceipts");
        }
    }
}
