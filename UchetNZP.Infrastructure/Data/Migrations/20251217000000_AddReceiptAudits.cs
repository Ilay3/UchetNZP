using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UchetNZP.Infrastructure.Data;

#nullable disable

namespace UchetNZP.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20251217000000_AddReceiptAudits")]
    public partial class AddReceiptAudits : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceiptAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpNumber = table.Column<int>(type: "integer", nullable: false),
                    PreviousQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    NewQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PreviousBalance = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    NewBalance = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    PreviousLabelId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewLabelId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousLabelAssigned = table.Column<bool>(type: "boolean", nullable: false),
                    NewLabelAssigned = table.Column<bool>(type: "boolean", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptAudits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptAudits_ReceiptId",
                table: "ReceiptAudits",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptAudits_VersionId",
                table: "ReceiptAudits",
                column: "VersionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceiptAudits");
        }
    }
}
