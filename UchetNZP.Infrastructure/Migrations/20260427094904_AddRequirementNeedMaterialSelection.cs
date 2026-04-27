using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequirementNeedMaterialSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "MetalMaterialId",
                table: "MetalRequirements",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ResolutionMessage",
                table: "MetalRequirements",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectionStatus",
                table: "MetalRequirements",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Resolved");

            migrationBuilder.AlterColumn<Guid>(
                name: "MetalMaterialId",
                table: "MetalRequirementItems",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.Sql("""
                UPDATE "MetalRequirements"
                SET "SelectionStatus" = 'Resolved'
                WHERE "SelectionStatus" = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResolutionMessage",
                table: "MetalRequirements");

            migrationBuilder.DropColumn(
                name: "SelectionStatus",
                table: "MetalRequirements");

            migrationBuilder.AlterColumn<Guid>(
                name: "MetalMaterialId",
                table: "MetalRequirements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "MetalMaterialId",
                table: "MetalRequirementItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
