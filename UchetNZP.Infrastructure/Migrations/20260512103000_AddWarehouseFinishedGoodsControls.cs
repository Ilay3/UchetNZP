using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UchetNZP.Infrastructure.Data;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260512103000_AddWarehouseFinishedGoodsControls")]
public partial class AddWarehouseFinishedGoodsControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "MovementType" character varying(32) NOT NULL DEFAULT 'Receipt';
            ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "SourceType" character varying(32) NOT NULL DEFAULT 'AutomaticTransfer';
            ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "DocumentNumber" character varying(64);
            ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "ControlCardNumber" character varying(64);
            ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "ControllerName" character varying(128);
            ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "MasterName" character varying(128);
            ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "AcceptedByName" character varying(128);
            ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "CreatedByUserId" uuid;

            CREATE INDEX IF NOT EXISTS "IX_WarehouseItems_MovementType"
                ON "WarehouseItems" ("MovementType");

            CREATE INDEX IF NOT EXISTS "IX_WarehouseItems_SourceType"
                ON "WarehouseItems" ("SourceType");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS "IX_WarehouseItems_SourceType";
            DROP INDEX IF EXISTS "IX_WarehouseItems_MovementType";

            ALTER TABLE "WarehouseItems" DROP COLUMN IF EXISTS "CreatedByUserId";
            ALTER TABLE "WarehouseItems" DROP COLUMN IF EXISTS "AcceptedByName";
            ALTER TABLE "WarehouseItems" DROP COLUMN IF EXISTS "MasterName";
            ALTER TABLE "WarehouseItems" DROP COLUMN IF EXISTS "ControllerName";
            ALTER TABLE "WarehouseItems" DROP COLUMN IF EXISTS "ControlCardNumber";
            ALTER TABLE "WarehouseItems" DROP COLUMN IF EXISTS "DocumentNumber";
            ALTER TABLE "WarehouseItems" DROP COLUMN IF EXISTS "SourceType";
            ALTER TABLE "WarehouseItems" DROP COLUMN IF EXISTS "MovementType";
            """);
    }
}
