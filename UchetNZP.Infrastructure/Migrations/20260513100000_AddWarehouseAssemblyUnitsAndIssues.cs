using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UchetNZP.Infrastructure.Data;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260513100000_AddWarehouseAssemblyUnitsAndIssues")]
public partial class AddWarehouseAssemblyUnitsAndIssues : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS "WarehouseAssemblyUnits" (
                "Id" uuid NOT NULL,
                "Name" character varying(256) NOT NULL,
                "NormalizedName" character varying(256) NOT NULL,
                "CreatedByUserId" uuid,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_WarehouseAssemblyUnits" PRIMARY KEY ("Id")
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_WarehouseAssemblyUnits_NormalizedName"
                ON "WarehouseAssemblyUnits" ("NormalizedName");

            ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "AssemblyUnitId" uuid;

            ALTER TABLE "WarehouseItems" DROP CONSTRAINT IF EXISTS "FK_WarehouseItems_Parts_PartId";
            ALTER TABLE "WarehouseItems" ALTER COLUMN "PartId" DROP NOT NULL;
            ALTER TABLE "WarehouseItems"
                ADD CONSTRAINT "FK_WarehouseItems_Parts_PartId"
                FOREIGN KEY ("PartId") REFERENCES "Parts" ("Id") ON DELETE SET NULL;

            DROP INDEX IF EXISTS "IX_WarehouseItems_AssemblyUnitId";
            CREATE INDEX "IX_WarehouseItems_AssemblyUnitId"
                ON "WarehouseItems" ("AssemblyUnitId");

            ALTER TABLE "WarehouseItems" DROP CONSTRAINT IF EXISTS "FK_WarehouseItems_WarehouseAssemblyUnits_AssemblyUnitId";
            ALTER TABLE "WarehouseItems"
                ADD CONSTRAINT "FK_WarehouseItems_WarehouseAssemblyUnits_AssemblyUnitId"
                FOREIGN KEY ("AssemblyUnitId") REFERENCES "WarehouseAssemblyUnits" ("Id") ON DELETE SET NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "WarehouseItems" DROP CONSTRAINT IF EXISTS "FK_WarehouseItems_WarehouseAssemblyUnits_AssemblyUnitId";
            ALTER TABLE "WarehouseItems" DROP CONSTRAINT IF EXISTS "FK_WarehouseItems_Parts_PartId";

            DELETE FROM "WarehouseItems" WHERE "PartId" IS NULL;
            ALTER TABLE "WarehouseItems" ALTER COLUMN "PartId" SET NOT NULL;
            ALTER TABLE "WarehouseItems"
                ADD CONSTRAINT "FK_WarehouseItems_Parts_PartId"
                FOREIGN KEY ("PartId") REFERENCES "Parts" ("Id") ON DELETE CASCADE;

            DROP INDEX IF EXISTS "IX_WarehouseItems_AssemblyUnitId";
            ALTER TABLE "WarehouseItems" DROP COLUMN IF EXISTS "AssemblyUnitId";

            DROP INDEX IF EXISTS "IX_WarehouseAssemblyUnits_NormalizedName";
            DROP TABLE IF EXISTS "WarehouseAssemblyUnits";
            """);
    }
}
