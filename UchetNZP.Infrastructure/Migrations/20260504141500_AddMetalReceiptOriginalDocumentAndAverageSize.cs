using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations;

public partial class AddMetalReceiptOriginalDocumentAndAverageSize : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "ReceiptNumber",
            table: "MetalReceipts",
            type: "character varying(128)",
            maxLength: 128,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(32)",
            oldMaxLength: 32);

        migrationBuilder.AddColumn<byte[]>(
            name: "OriginalDocumentContent",
            table: "MetalReceipts",
            type: "bytea",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OriginalDocumentContentType",
            table: "MetalReceipts",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OriginalDocumentFileName",
            table: "MetalReceipts",
            type: "character varying(260)",
            maxLength: 260,
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "OriginalDocumentSizeBytes",
            table: "MetalReceipts",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "OriginalDocumentUploadedAt",
            table: "MetalReceipts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "GeneratedCode",
            table: "MetalReceiptItems",
            type: "character varying(128)",
            maxLength: 128,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(64)",
            oldMaxLength: 64);

        migrationBuilder.AddColumn<bool>(
            name: "IsSizeApproximate",
            table: "MetalReceiptItems",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "ReceiptLineIndex",
            table: "MetalReceiptItems",
            type: "integer",
            nullable: false,
            defaultValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "OriginalDocumentContent",
            table: "MetalReceipts");

        migrationBuilder.DropColumn(
            name: "OriginalDocumentContentType",
            table: "MetalReceipts");

        migrationBuilder.DropColumn(
            name: "OriginalDocumentFileName",
            table: "MetalReceipts");

        migrationBuilder.DropColumn(
            name: "OriginalDocumentSizeBytes",
            table: "MetalReceipts");

        migrationBuilder.DropColumn(
            name: "OriginalDocumentUploadedAt",
            table: "MetalReceipts");

        migrationBuilder.DropColumn(
            name: "IsSizeApproximate",
            table: "MetalReceiptItems");

        migrationBuilder.DropColumn(
            name: "ReceiptLineIndex",
            table: "MetalReceiptItems");

        migrationBuilder.AlterColumn<string>(
            name: "ReceiptNumber",
            table: "MetalReceipts",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(128)",
            oldMaxLength: 128);

        migrationBuilder.AlterColumn<string>(
            name: "GeneratedCode",
            table: "MetalReceiptItems",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(128)",
            oldMaxLength: 128);
    }
}
