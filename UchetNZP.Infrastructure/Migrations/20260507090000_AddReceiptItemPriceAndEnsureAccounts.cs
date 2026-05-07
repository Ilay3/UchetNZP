using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations;

public partial class AddReceiptItemPriceAndEnsureAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"MetalReceipts\" ADD COLUMN IF NOT EXISTS \"AccountingAccount\" character varying(16) NOT NULL DEFAULT '10.01';");
        migrationBuilder.Sql("ALTER TABLE \"MetalReceipts\" ADD COLUMN IF NOT EXISTS \"VatAccount\" character varying(16) NOT NULL DEFAULT '19.03';");
        migrationBuilder.Sql("ALTER TABLE \"MetalReceiptItems\" ADD COLUMN IF NOT EXISTS \"PricePerKg\" numeric(18,4) NOT NULL DEFAULT 0;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"MetalReceiptItems\" DROP COLUMN IF EXISTS \"PricePerKg\";");
    }
}
