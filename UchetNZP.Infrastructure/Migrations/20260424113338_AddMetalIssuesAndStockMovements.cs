using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMetalIssuesAndStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetalIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MetalRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Comment = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetalIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetalIssues_MetalRequirements_MetalRequirementId",
                        column: x => x.MetalRequirementId,
                        principalTable: "MetalRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MetalStockMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MovementType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    MetalMaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetalReceiptItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceDocumentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    QtyBefore = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    QtyChange = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    QtyAfter = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    Unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Comment = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetalStockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetalStockMovements_MetalMaterials_MetalMaterialId",
                        column: x => x.MetalMaterialId,
                        principalTable: "MetalMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MetalStockMovements_MetalReceiptItems_MetalReceiptItemId",
                        column: x => x.MetalReceiptItemId,
                        principalTable: "MetalReceiptItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MetalIssueItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MetalIssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetalReceiptItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceQtyBefore = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    IssuedQty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    RemainingQtyAfter = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LineStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetalIssueItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetalIssueItems_MetalIssues_MetalIssueId",
                        column: x => x.MetalIssueId,
                        principalTable: "MetalIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MetalIssueItems_MetalReceiptItems_MetalReceiptItemId",
                        column: x => x.MetalReceiptItemId,
                        principalTable: "MetalReceiptItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetalIssueItems_MetalIssueId_SortOrder",
                table: "MetalIssueItems",
                columns: new[] { "MetalIssueId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MetalIssueItems_MetalReceiptItemId",
                table: "MetalIssueItems",
                column: "MetalReceiptItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MetalIssues_IssueNumber",
                table: "MetalIssues",
                column: "IssueNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetalIssues_MetalRequirementId",
                table: "MetalIssues",
                column: "MetalRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_MetalStockMovements_MetalMaterialId",
                table: "MetalStockMovements",
                column: "MetalMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MetalStockMovements_MetalReceiptItemId",
                table: "MetalStockMovements",
                column: "MetalReceiptItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MetalStockMovements_MovementDate",
                table: "MetalStockMovements",
                column: "MovementDate");

            migrationBuilder.CreateIndex(
                name: "IX_MetalStockMovements_SourceDocumentType_SourceDocumentId",
                table: "MetalStockMovements",
                columns: new[] { "SourceDocumentType", "SourceDocumentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetalIssueItems");

            migrationBuilder.DropTable(
                name: "MetalStockMovements");

            migrationBuilder.DropTable(
                name: "MetalIssues");
        }
    }
}
