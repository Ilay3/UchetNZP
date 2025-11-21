using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UchetNZP.Infrastructure.Data;

#nullable disable

namespace UchetNZP.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20251220000000_AddTransferAudits")]
    public partial class AddTransferAudits : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransferAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromOpNumber = table.Column<int>(type: "integer", nullable: false),
                    ToSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToOpNumber = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Comment = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TransferDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromBalanceBefore = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    FromBalanceAfter = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    ToBalanceBefore = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    ToBalanceAfter = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    IsWarehouseTransfer = table.Column<bool>(type: "boolean", nullable: false),
                    WipLabelId = table.Column<Guid>(type: "uuid", nullable: true),
                    LabelNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LabelQuantityBefore = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    LabelQuantityAfter = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    ScrapQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    ScrapType = table.Column<int>(type: "integer", nullable: true),
                    ScrapComment = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsReverted = table.Column<bool>(type: "boolean", nullable: false),
                    RevertedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransferAuditOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferAuditId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpNumber = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartRouteId = table.Column<Guid>(type: "uuid", nullable: true),
                    BalanceBefore = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    QuantityChange = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    IsWarehouse = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferAuditOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferAuditOperations_TransferAudits_TransferAuditId",
                        column: x => x.TransferAuditId,
                        principalTable: "TransferAudits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransferAuditOperations_TransferAuditId",
                table: "TransferAuditOperations",
                column: "TransferAuditId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferAudits_TransferId",
                table: "TransferAudits",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferAudits_TransactionId",
                table: "TransferAudits",
                column: "TransactionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransferAuditOperations");

            migrationBuilder.DropTable(
                name: "TransferAudits");
        }
    }
}
