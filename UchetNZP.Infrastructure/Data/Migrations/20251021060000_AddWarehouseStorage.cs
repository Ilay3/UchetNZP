using System;
using Microsoft.EntityFrameworkCore.Migrations;
using UchetNZP.Shared;

#nullable disable

namespace UchetNZP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Sections",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[] { WarehouseDefaults.SectionId, null, WarehouseDefaults.SectionName });

            migrationBuilder.InsertData(
                table: "Operations",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[] { WarehouseDefaults.OperationId, null, WarehouseDefaults.OperationName });

            migrationBuilder.CreateTable(
                name: "WarehouseItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseItems_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WarehouseItems_WipTransfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "WipTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseItems_PartId",
                table: "WarehouseItems",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseItems_TransferId",
                table: "WarehouseItems",
                column: "TransferId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WarehouseItems");

            migrationBuilder.DeleteData(
                table: "Operations",
                keyColumn: "Id",
                keyValue: WarehouseDefaults.OperationId);

            migrationBuilder.DeleteData(
                table: "Sections",
                keyColumn: "Id",
                keyValue: WarehouseDefaults.SectionId);
        }
    }
}
