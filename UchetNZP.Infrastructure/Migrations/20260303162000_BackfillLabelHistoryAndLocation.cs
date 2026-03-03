using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations;

public partial class BackfillLabelHistoryAndLocation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            -- 1) Root/lineage fields backfill from Number for legacy rows.
            UPDATE "WipLabels" l
            SET "RootLabelId" = CASE WHEN l."RootLabelId" = '00000000-0000-0000-0000-000000000000'::uuid THEN l."Id" ELSE l."RootLabelId" END,
                "ParentLabelId" = COALESCE(l."ParentLabelId", NULL),
                "RootNumber" = CASE
                    WHEN COALESCE(TRIM(l."RootNumber"), '') <> '' THEN l."RootNumber"
                    WHEN POSITION('/' IN l."Number") > 0 THEN SPLIT_PART(l."Number", '/', 1)
                    ELSE l."Number"
                END,
                "Suffix" = CASE
                    WHEN l."Suffix" > 0 THEN l."Suffix"
                    WHEN POSITION('/' IN l."Number") > 0 AND SPLIT_PART(l."Number", '/', 2) ~ '^[0-9]+$' THEN SPLIT_PART(l."Number", '/', 2)::integer
                    ELSE 0
                END,
                "Status" = CASE
                    WHEN l."Status" IS NULL OR l."Status" = '' THEN CASE WHEN l."RemainingQuantity" <= 0 THEN 'Consumed' ELSE 'Active' END
                    ELSE l."Status"
                END;

            -- 2) Current section/op recovery from latest non-reverted transfer audit, then receipt fallback.
            WITH latest_transfer AS (
                SELECT DISTINCT ON (ta."WipLabelId")
                    ta."WipLabelId" AS "LabelId",
                    ta."ToSectionId",
                    ta."ToOpNumber",
                    ta."TransferDate",
                    ta."CreatedAt",
                    ta."Id"
                FROM "TransferAudits" ta
                WHERE ta."WipLabelId" IS NOT NULL AND COALESCE(ta."IsReverted", FALSE) = FALSE
                ORDER BY ta."WipLabelId", ta."TransferDate" DESC, ta."CreatedAt" DESC, ta."Id" DESC
            )
            UPDATE "WipLabels" l
            SET "CurrentSectionId" = lt."ToSectionId",
                "CurrentOpNumber" = lt."ToOpNumber"
            FROM latest_transfer lt
            WHERE lt."LabelId" = l."Id";

            UPDATE "WipLabels" l
            SET "CurrentSectionId" = r."SectionId",
                "CurrentOpNumber" = r."OpNumber"
            FROM "WipReceipts" r
            WHERE r."WipLabelId" = l."Id"
              AND l."CurrentSectionId" IS NULL;

            -- 3) Backfill ledger from receipts/transfers/scrap using deterministic EventId to avoid duplicates.
            INSERT INTO "WipLabelLedger"
            (
                "EventId", "EventTime", "UserId", "TransactionId", "EventType",
                "FromLabelId", "ToLabelId", "FromSectionId", "FromOpNumber", "ToSectionId", "ToOpNumber",
                "Qty", "ScrapQty", "RefEntityType", "RefEntityId"
            )
            SELECT
                (
                    substr(md5('receipt:' || r."Id"::text), 1, 8) || '-' ||
                    substr(md5('receipt:' || r."Id"::text), 9, 4) || '-' ||
                    substr(md5('receipt:' || r."Id"::text), 13, 4) || '-' ||
                    substr(md5('receipt:' || r."Id"::text), 17, 4) || '-' ||
                    substr(md5('receipt:' || r."Id"::text), 21, 12)
                )::uuid,
                COALESCE(r."CreatedAt", r."ReceiptDate", NOW()),
                COALESCE(r."UserId", '00000000-0000-0000-0000-000000000000'::uuid),
                r."Id",
                'Receipt',
                r."WipLabelId",
                r."WipLabelId",
                r."SectionId",
                r."OpNumber",
                r."SectionId",
                r."OpNumber",
                COALESCE(r."Quantity", 0),
                0,
                'WipReceipt',
                r."Id"
            FROM "WipReceipts" r
            WHERE r."WipLabelId" IS NOT NULL
              AND NOT EXISTS (
                    SELECT 1 FROM "WipLabelLedger" l
                    WHERE l."RefEntityType" = 'WipReceipt' AND l."RefEntityId" = r."Id"
              );

            INSERT INTO "WipLabelLedger"
            (
                "EventId", "EventTime", "UserId", "TransactionId", "EventType",
                "FromLabelId", "ToLabelId", "FromSectionId", "FromOpNumber", "ToSectionId", "ToOpNumber",
                "Qty", "ScrapQty", "RefEntityType", "RefEntityId"
            )
            SELECT
                (
                    substr(md5('transfer:' || ta."TransferId"::text), 1, 8) || '-' ||
                    substr(md5('transfer:' || ta."TransferId"::text), 9, 4) || '-' ||
                    substr(md5('transfer:' || ta."TransferId"::text), 13, 4) || '-' ||
                    substr(md5('transfer:' || ta."TransferId"::text), 17, 4) || '-' ||
                    substr(md5('transfer:' || ta."TransferId"::text), 21, 12)
                )::uuid,
                COALESCE(ta."CreatedAt", ta."TransferDate", NOW()),
                COALESCE(ta."UserId", '00000000-0000-0000-0000-000000000000'::uuid),
                ta."TransactionId",
                CASE WHEN COALESCE(ta."ResidualLabelQuantity", 0) > 0 THEN 'Split' ELSE 'Transfer' END,
                ta."WipLabelId",
                COALESCE(ta."ResidualWipLabelId", ta."WipLabelId"),
                ta."FromSectionId",
                ta."FromOpNumber",
                ta."ToSectionId",
                ta."ToOpNumber",
                COALESCE(ta."Quantity", 0),
                COALESCE(ta."ScrapQuantity", 0),
                'WipTransfer',
                ta."TransferId"
            FROM "TransferAudits" ta
            WHERE ta."WipLabelId" IS NOT NULL
              AND COALESCE(ta."IsReverted", FALSE) = FALSE
              AND NOT EXISTS (
                    SELECT 1 FROM "WipLabelLedger" l
                    WHERE l."RefEntityType" = 'WipTransfer' AND l."RefEntityId" = ta."TransferId"
              );

            INSERT INTO "WipLabelLedger"
            (
                "EventId", "EventTime", "UserId", "TransactionId", "EventType",
                "FromLabelId", "ToLabelId", "FromSectionId", "FromOpNumber", "ToSectionId", "ToOpNumber",
                "Qty", "ScrapQty", "RefEntityType", "RefEntityId"
            )
            SELECT
                (
                    substr(md5('scrap:' || s."Id"::text), 1, 8) || '-' ||
                    substr(md5('scrap:' || s."Id"::text), 9, 4) || '-' ||
                    substr(md5('scrap:' || s."Id"::text), 13, 4) || '-' ||
                    substr(md5('scrap:' || s."Id"::text), 17, 4) || '-' ||
                    substr(md5('scrap:' || s."Id"::text), 21, 12)
                )::uuid,
                COALESCE(s."RecordedAt", NOW()),
                COALESCE(s."UserId", '00000000-0000-0000-0000-000000000000'::uuid),
                COALESCE(s."TransferId", s."Id"),
                'Scrap',
                ta."WipLabelId",
                ta."WipLabelId",
                s."SectionId",
                s."OpNumber",
                s."SectionId",
                s."OpNumber",
                0,
                COALESCE(s."Quantity", 0),
                'WipScrap',
                s."Id"
            FROM "WipScraps" s
            LEFT JOIN "TransferAudits" ta ON ta."TransferId" = s."TransferId" AND COALESCE(ta."IsReverted", FALSE) = FALSE
            WHERE ta."WipLabelId" IS NOT NULL
              AND NOT EXISTS (
                    SELECT 1 FROM "WipLabelLedger" l
                    WHERE l."RefEntityType" = 'WipScrap' AND l."RefEntityId" = s."Id"
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Data backfill migration is not reverted to avoid history loss.
    }
}
