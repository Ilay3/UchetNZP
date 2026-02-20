using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UchetNZP.Infrastructure.Data;

#nullable disable

namespace UchetNZP.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260220000000_AddBulkCleanupAndLabelYear")]
    public partial class AddBulkCleanupAndLabelYear : Migration
    {
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WipBalanceCleanupJobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                PartId = table.Column<Guid>(type: "uuid", nullable: true),
                SectionId = table.Column<Guid>(type: "uuid", nullable: true),
                OpNumber = table.Column<int>(type: "integer", nullable: true),
                MinQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                AffectedCount = table.Column<int>(type: "integer", nullable: false),
                AffectedQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                Comment = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                IsExecuted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WipBalanceCleanupJobs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "WipBalanceCleanupStageItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                JobId = table.Column<Guid>(type: "uuid", nullable: false),
                WipBalanceId = table.Column<Guid>(type: "uuid", nullable: false),
                PreviousQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WipBalanceCleanupStageItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_WipBalanceCleanupStageItems_WipBalanceCleanupJobs_JobId",
                    column: x => x.JobId,
                    principalTable: "WipBalanceCleanupJobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_WipBalanceCleanupStageItems_WipBalances_WipBalanceId",
                    column: x => x.WipBalanceId,
                    principalTable: "WipBalances",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_WipBalanceCleanupJobs_CreatedAt",
            table: "WipBalanceCleanupJobs",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_WipBalanceCleanupStageItems_JobId_WipBalanceId",
            table: "WipBalanceCleanupStageItems",
            columns: new[] { "JobId", "WipBalanceId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WipBalanceCleanupStageItems_WipBalanceId",
            table: "WipBalanceCleanupStageItems",
            column: "WipBalanceId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "WipBalanceCleanupStageItems");
        migrationBuilder.DropTable(name: "WipBalanceCleanupJobs");
    }
    }
}
