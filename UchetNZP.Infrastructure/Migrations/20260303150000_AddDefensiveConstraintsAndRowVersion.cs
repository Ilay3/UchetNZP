using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations;

public partial class AddDefensiveConstraintsAndRowVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "WipLabels",
            type: "bytea",
            rowVersion: true,
            nullable: false,
            defaultValue: Array.Empty<byte>());

        migrationBuilder.AddColumn<decimal>(
            name: "RemainingBefore",
            table: "TransferLabelUsages",
            type: "numeric(12,3)",
            precision: 12,
            scale: 3,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddCheckConstraint(
            name: "CK_WipLabels_Quantity_Positive",
            table: "WipLabels",
            sql: "\"Quantity\" > 0");

        migrationBuilder.AddCheckConstraint(
            name: "CK_WipLabels_Remaining_NonNegative",
            table: "WipLabels",
            sql: "\"RemainingQuantity\" >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "CK_WipLabels_Remaining_NotGreaterThanQuantity",
            table: "WipLabels",
            sql: "\"RemainingQuantity\" <= \"Quantity\"");

        migrationBuilder.AddCheckConstraint(
            name: "CK_TransferLabelUsages_Qty_NonNegative",
            table: "TransferLabelUsages",
            sql: "\"Qty\" >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "CK_TransferLabelUsages_ScrapQty_NonNegative",
            table: "TransferLabelUsages",
            sql: "\"ScrapQty\" >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "CK_TransferLabelUsages_RemainingBefore_NonNegative",
            table: "TransferLabelUsages",
            sql: "\"RemainingBefore\" >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "CK_TransferLabelUsages_Consumption_WithinRemaining",
            table: "TransferLabelUsages",
            sql: "(\"Qty\" + \"ScrapQty\") <= \"RemainingBefore\"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "CK_TransferLabelUsages_Consumption_WithinRemaining", table: "TransferLabelUsages");
        migrationBuilder.DropCheckConstraint(name: "CK_TransferLabelUsages_RemainingBefore_NonNegative", table: "TransferLabelUsages");
        migrationBuilder.DropCheckConstraint(name: "CK_TransferLabelUsages_ScrapQty_NonNegative", table: "TransferLabelUsages");
        migrationBuilder.DropCheckConstraint(name: "CK_TransferLabelUsages_Qty_NonNegative", table: "TransferLabelUsages");
        migrationBuilder.DropCheckConstraint(name: "CK_WipLabels_Remaining_NotGreaterThanQuantity", table: "WipLabels");
        migrationBuilder.DropCheckConstraint(name: "CK_WipLabels_Remaining_NonNegative", table: "WipLabels");
        migrationBuilder.DropCheckConstraint(name: "CK_WipLabels_Quantity_Positive", table: "WipLabels");

        migrationBuilder.DropColumn(name: "RemainingBefore", table: "TransferLabelUsages");
        migrationBuilder.DropColumn(name: "RowVersion", table: "WipLabels");
    }
}
