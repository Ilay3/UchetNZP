using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWipBalanceAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WipBalanceAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WipBalanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpNumber = table.Column<int>(type: "integer", nullable: false),
                    PreviousQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    NewQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Delta = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Comment = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipBalanceAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WipBalanceAdjustments_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WipBalanceAdjustments_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WipBalanceAdjustments_WipBalances_WipBalanceId",
                        column: x => x.WipBalanceId,
                        principalTable: "WipBalances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WipBalanceAdjustments_PartId",
                table: "WipBalanceAdjustments",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_WipBalanceAdjustments_SectionId",
                table: "WipBalanceAdjustments",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WipBalanceAdjustments_WipBalanceId",
                table: "WipBalanceAdjustments",
                column: "WipBalanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WipBalanceAdjustments");
        }
    }
}
