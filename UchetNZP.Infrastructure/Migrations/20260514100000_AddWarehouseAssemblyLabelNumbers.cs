using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UchetNZP.Infrastructure.Data;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260514100000_AddWarehouseAssemblyLabelNumbers")]
public partial class AddWarehouseAssemblyLabelNumbers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "WarehouseLabelItems"
                ADD COLUMN IF NOT EXISTS "LabelNumber" character varying(32);

            UPDATE "WarehouseLabelItems" AS item
            SET "LabelNumber" = label."Number"
            FROM "WipLabels" AS label
            WHERE item."WipLabelId" = label."Id"
              AND (item."LabelNumber" IS NULL OR item."LabelNumber" = '');

            ALTER TABLE "WarehouseLabelItems"
                DROP CONSTRAINT IF EXISTS "FK_WarehouseLabelItems_WipLabels_WipLabelId";

            ALTER TABLE "WarehouseLabelItems"
                ALTER COLUMN "WipLabelId" DROP NOT NULL;

            ALTER TABLE "WarehouseLabelItems"
                ADD CONSTRAINT "FK_WarehouseLabelItems_WipLabels_WipLabelId"
                FOREIGN KEY ("WipLabelId") REFERENCES "WipLabels" ("Id") ON DELETE SET NULL;

            CREATE INDEX IF NOT EXISTS "IX_WarehouseLabelItems_LabelNumber"
                ON "WarehouseLabelItems" ("LabelNumber");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM "WarehouseLabelItems"
            WHERE "WipLabelId" IS NULL;

            DROP INDEX IF EXISTS "IX_WarehouseLabelItems_LabelNumber";

            ALTER TABLE "WarehouseLabelItems"
                DROP CONSTRAINT IF EXISTS "FK_WarehouseLabelItems_WipLabels_WipLabelId";

            ALTER TABLE "WarehouseLabelItems"
                ALTER COLUMN "WipLabelId" SET NOT NULL;

            ALTER TABLE "WarehouseLabelItems"
                ADD CONSTRAINT "FK_WarehouseLabelItems_WipLabels_WipLabelId"
                FOREIGN KEY ("WipLabelId") REFERENCES "WipLabels" ("Id") ON DELETE CASCADE;

            ALTER TABLE "WarehouseLabelItems"
                DROP COLUMN IF EXISTS "LabelNumber";
            """);
    }
}
