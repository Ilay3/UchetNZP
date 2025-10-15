using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWipAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "WipReceipts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "WipReceipts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "WipReceipts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "NormHours",
                table: "WipLaunchOperations",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "WipLaunches",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "WipLaunches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "FromOpNumber",
                table: "WipLaunches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "WipLaunches",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Comment",
                table: "WipReceipts");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "WipReceipts");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "WipReceipts");

            migrationBuilder.DropColumn(
                name: "NormHours",
                table: "WipLaunchOperations");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "WipLaunches");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "WipLaunches");

            migrationBuilder.DropColumn(
                name: "FromOpNumber",
                table: "WipLaunches");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "WipLaunches");
        }
    }
}
