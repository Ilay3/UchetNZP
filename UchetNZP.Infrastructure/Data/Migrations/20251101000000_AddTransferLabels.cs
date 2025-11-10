using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using UchetNZP.Infrastructure.Data;

#nullable disable

namespace UchetNZP.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20251101000000_AddTransferLabels")]
    public partial class AddTransferLabels : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WipLabelId",
                table: "WipTransfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingQuantity",
                table: "WipLabels",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_WipTransfers_WipLabelId",
                table: "WipTransfers",
                column: "WipLabelId");

            migrationBuilder.AddForeignKey(
                name: "FK_WipTransfers_WipLabels_WipLabelId",
                table: "WipTransfers",
                column: "WipLabelId",
                principalTable: "WipLabels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql("UPDATE \"WipLabels\" SET \"RemainingQuantity\" = \"Quantity\" WHERE \"RemainingQuantity\" = 0;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WipTransfers_WipLabels_WipLabelId",
                table: "WipTransfers");

            migrationBuilder.DropIndex(
                name: "IX_WipTransfers_WipLabelId",
                table: "WipTransfers");

            migrationBuilder.DropColumn(
                name: "WipLabelId",
                table: "WipTransfers");

            migrationBuilder.DropColumn(
                name: "RemainingQuantity",
                table: "WipLabels");
        }
    }
}
