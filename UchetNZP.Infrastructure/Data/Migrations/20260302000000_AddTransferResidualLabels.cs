using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Data.Migrations;

public partial class AddTransferResidualLabels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ResidualLabelNumber",
            table: "TransferAudits",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "ResidualLabelQuantity",
            table: "TransferAudits",
            type: "numeric(12,3)",
            precision: 12,
            scale: 3,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ResidualWipLabelId",
            table: "TransferAudits",
            type: "uuid",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ResidualLabelNumber",
            table: "TransferAudits");

        migrationBuilder.DropColumn(
            name: "ResidualLabelQuantity",
            table: "TransferAudits");

        migrationBuilder.DropColumn(
            name: "ResidualWipLabelId",
            table: "TransferAudits");
    }
}
