using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWipReceiptMetalMaterial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MetalMaterialId",
                table: "WipReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WipReceipts_MetalMaterialId",
                table: "WipReceipts",
                column: "MetalMaterialId");

            migrationBuilder.AddForeignKey(
                name: "FK_WipReceipts_MetalMaterials_MetalMaterialId",
                table: "WipReceipts",
                column: "MetalMaterialId",
                principalTable: "MetalMaterials",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WipReceipts_MetalMaterials_MetalMaterialId",
                table: "WipReceipts");

            migrationBuilder.DropIndex(
                name: "IX_WipReceipts_MetalMaterialId",
                table: "WipReceipts");

            migrationBuilder.DropColumn(
                name: "MetalMaterialId",
                table: "WipReceipts");
        }
    }
}
