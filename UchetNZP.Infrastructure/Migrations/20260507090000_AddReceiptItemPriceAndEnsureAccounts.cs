using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations;

public partial class AddReceiptItemPriceAndEnsureAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"MetalReceipts\" ADD COLUMN IF NOT EXISTS \"AccountingAccount\" character varying(16) NOT NULL DEFAULT '10.01';");
        migrationBuilder.Sql("ALTER TABLE \"MetalReceipts\" ADD COLUMN IF NOT EXISTS \"VatAccount\" character varying(16) NOT NULL DEFAULT '19.03';");

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = current_schema()
                      AND table_name = 'MetalReceiptItems'
                      AND column_name = 'priceperkg'
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = current_schema()
                      AND table_name = 'MetalReceiptItems'
                      AND column_name = 'PricePerKg'
                ) THEN
                    EXECUTE 'ALTER TABLE "MetalReceiptItems" RENAME COLUMN priceperkg TO "PricePerKg"';
                END IF;

                IF NOT EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = current_schema()
                      AND table_name = 'MetalReceiptItems'
                      AND column_name = 'PricePerKg'
                ) THEN
                    EXECUTE 'ALTER TABLE "MetalReceiptItems" ADD COLUMN "PricePerKg" numeric(18,4) NOT NULL DEFAULT 0';
                END IF;
            END
            $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"MetalReceiptItems\" DROP COLUMN IF EXISTS \"PricePerKg\";");
    }
}
