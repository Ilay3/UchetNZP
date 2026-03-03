using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWipLabelLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WipLabelLedger",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FromLabelId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToLabelId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromSectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromOpNumber = table.Column<int>(type: "integer", nullable: true),
                    ToSectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToOpNumber = table.Column<int>(type: "integer", nullable: true),
                    Qty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    ScrapQty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    RefEntityType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RefEntityId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipLabelLedger", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WipLabelLedger_EventTime",
                table: "WipLabelLedger",
                column: "EventTime");

            migrationBuilder.CreateIndex(
                name: "IX_WipLabelLedger_FromLabelId",
                table: "WipLabelLedger",
                column: "FromLabelId");

            migrationBuilder.CreateIndex(
                name: "IX_WipLabelLedger_ToLabelId",
                table: "WipLabelLedger",
                column: "ToLabelId");

            migrationBuilder.CreateIndex(
                name: "IX_WipLabelLedger_TransactionId",
                table: "WipLabelLedger",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WipLabelLedger");
        }
    }
}
