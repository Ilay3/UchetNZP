using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations;

public partial class FixWipLabelRowVersionNullability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<byte[]>(
            name: "RowVersion",
            table: "WipLabels",
            type: "bytea",
            nullable: true,
            defaultValueSql: "'\\x'::bytea",
            oldClrType: typeof(byte[]),
            oldType: "bytea");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE \"WipLabels\" SET \"RowVersion\" = '\\x'::bytea WHERE \"RowVersion\" IS NULL;");

        migrationBuilder.AlterColumn<byte[]>(
            name: "RowVersion",
            table: "WipLabels",
            type: "bytea",
            nullable: false,
            defaultValue: System.Array.Empty<byte>(),
            oldClrType: typeof(byte[]),
            oldType: "bytea",
            oldNullable: true,
            oldDefaultValueSql: "'\\x'::bytea");
    }
}
