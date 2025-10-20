using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWipScrap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WipTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromOpNumber = table.Column<int>(type: "integer", nullable: false),
                    ToSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToOpNumber = table.Column<int>(type: "integer", nullable: false),
                    TransferDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WipTransfers_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WipScraps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpNumber = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    ScrapType = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipScraps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WipScraps_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WipScraps_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WipScraps_WipTransfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "WipTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WipTransferOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WipTransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpNumber = table.Column<int>(type: "integer", nullable: false),
                    PartRouteId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuantityChange = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WipTransferOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WipTransferOperations_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WipTransferOperations_PartRoutes_PartRouteId",
                        column: x => x.PartRouteId,
                        principalTable: "PartRoutes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WipTransferOperations_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WipTransferOperations_WipTransfers_WipTransferId",
                        column: x => x.WipTransferId,
                        principalTable: "WipTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WipScraps_PartId_OpNumber",
                table: "WipScraps",
                columns: new[] { "PartId", "OpNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_WipScraps_SectionId",
                table: "WipScraps",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WipScraps_TransferId",
                table: "WipScraps",
                column: "TransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WipTransferOperations_OperationId",
                table: "WipTransferOperations",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_WipTransferOperations_PartRouteId",
                table: "WipTransferOperations",
                column: "PartRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_WipTransferOperations_SectionId",
                table: "WipTransferOperations",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WipTransferOperations_WipTransferId",
                table: "WipTransferOperations",
                column: "WipTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_WipTransfers_PartId",
                table: "WipTransfers",
                column: "PartId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WipScraps");

            migrationBuilder.DropTable(
                name: "WipTransferOperations");

            migrationBuilder.DropTable(
                name: "WipTransfers");
        }
    }
}
