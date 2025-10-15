using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateImportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImportJobItems_ImportJobId_ExternalId",
                table: "ImportJobItems");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "ImportJobs",
                newName: "Ts");

            migrationBuilder.RenameColumn(
                name: "ErrorMessage",
                table: "ImportJobItems",
                newName: "Message");

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "ImportJobs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Skipped",
                table: "ImportJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Succeeded",
                table: "ImportJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalRows",
                table: "ImportJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "ImportJobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "RowIndex",
                table: "ImportJobItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE \"ImportJobItems\" SET \"RowIndex\" = CASE WHEN \"ExternalId\" ~ '^[0-9]+$' THEN CAST(\"ExternalId\" AS integer) ELSE 0 END;");

            migrationBuilder.Sql(
                "UPDATE \"ImportJobItems\" SET \"Status\" = 'Succeeded' WHERE \"Status\" = 'Saved';");

            migrationBuilder.Sql(
                @"UPDATE ""ImportJobs"" AS j
SET ""Succeeded"" = COALESCE(t.""SucceededCount"", 0),
    ""Skipped"" = COALESCE(t.""SkippedCount"", 0),
    ""TotalRows"" = COALESCE(t.""SucceededCount"", 0) + COALESCE(t.""SkippedCount"", 0)
FROM (
    SELECT ""ImportJobId"",
           COUNT(*) FILTER (WHERE ""Status"" = 'Succeeded') AS ""SucceededCount"",
           COUNT(*) FILTER (WHERE ""Status"" = 'Skipped') AS ""SkippedCount""
    FROM ""ImportJobItems""
    GROUP BY ""ImportJobId""
) AS t
WHERE j.""Id"" = t.""ImportJobId"";");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ImportJobItems");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "ImportJobItems");

            migrationBuilder.DropColumn(
                name: "Payload",
                table: "ImportJobItems");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "ImportJobItems");

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobItems_ImportJobId_RowIndex",
                table: "ImportJobItems",
                columns: new[] { "ImportJobId", "RowIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImportJobItems_ImportJobId_RowIndex",
                table: "ImportJobItems");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "Skipped",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "Succeeded",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "TotalRows",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "RowIndex",
                table: "ImportJobItems");

            migrationBuilder.RenameColumn(
                name: "Ts",
                table: "ImportJobs",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "Message",
                table: "ImportJobItems",
                newName: "ErrorMessage");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "ImportJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "ImportJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ImportJobs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "ImportJobs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ImportJobItems",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "ImportJobItems",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Payload",
                table: "ImportJobItems",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAt",
                table: "ImportJobItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobItems_ImportJobId_ExternalId",
                table: "ImportJobItems",
                columns: new[] { "ImportJobId", "ExternalId" },
                unique: true);
        }
    }
}
