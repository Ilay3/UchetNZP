using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations;

public partial class AddTransferLabelUsage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TransferLabelUsages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                FromLabelId = table.Column<Guid>(type: "uuid", nullable: false),
                Qty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                ScrapQty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                CreatedToLabelId = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TransferLabelUsages", x => x.Id);
                table.ForeignKey(
                    name: "FK_TransferLabelUsages_WipLabels_CreatedToLabelId",
                    column: x => x.CreatedToLabelId,
                    principalTable: "WipLabels",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_TransferLabelUsages_WipLabels_FromLabelId",
                    column: x => x.FromLabelId,
                    principalTable: "WipLabels",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_TransferLabelUsages_WipTransfers_TransferId",
                    column: x => x.TransferId,
                    principalTable: "WipTransfers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TransferLabelUsages_CreatedToLabelId",
            table: "TransferLabelUsages",
            column: "CreatedToLabelId");

        migrationBuilder.CreateIndex(
            name: "IX_TransferLabelUsages_FromLabelId",
            table: "TransferLabelUsages",
            column: "FromLabelId");

        migrationBuilder.CreateIndex(
            name: "IX_TransferLabelUsages_TransferId",
            table: "TransferLabelUsages",
            column: "TransferId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TransferLabelUsages");
    }
}
