using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMetalWarehouseReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetalMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UnitKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetalMaterials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetalReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReceiptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupplierOrSource = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Comment = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetalReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetalReceiptItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MetalReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetalMaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    TotalWeightKg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    ItemIndex = table.Column<int>(type: "integer", nullable: false),
                    SizeValue = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    SizeUnitText = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    GeneratedCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetalReceiptItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetalReceiptItems_MetalMaterials_MetalMaterialId",
                        column: x => x.MetalMaterialId,
                        principalTable: "MetalMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MetalReceiptItems_MetalReceipts_MetalReceiptId",
                        column: x => x.MetalReceiptId,
                        principalTable: "MetalReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetalMaterials_Code",
                table: "MetalMaterials",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetalReceiptItems_MetalMaterialId",
                table: "MetalReceiptItems",
                column: "MetalMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MetalReceiptItems_MetalReceiptId_ItemIndex",
                table: "MetalReceiptItems",
                columns: new[] { "MetalReceiptId", "ItemIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetalReceipts_ReceiptDate",
                table: "MetalReceipts",
                column: "ReceiptDate");

            migrationBuilder.CreateIndex(
                name: "IX_MetalReceipts_ReceiptNumber",
                table: "MetalReceipts",
                column: "ReceiptNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetalReceiptItems");

            migrationBuilder.DropTable(
                name: "MetalMaterials");

            migrationBuilder.DropTable(
                name: "MetalReceipts");
        }
    }
}
