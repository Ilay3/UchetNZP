using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWipLabelStateAndLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentOpNumber",
                table: "WipLabels",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentSectionId",
                table: "WipLabels",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentLabelId",
                table: "WipLabels",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RootLabelId",
                table: "WipLabels",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "RootNumber",
                table: "WipLabels",
                type: "character varying(11)",
                maxLength: 11,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "WipLabels",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<int>(
                name: "Suffix",
                table: "WipLabels",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_WipLabels_RootLabelId",
                table: "WipLabels",
                column: "RootLabelId");

            migrationBuilder.CreateIndex(
                name: "IX_WipLabels_Status_CurrentSectionId_CurrentOpNumber",
                table: "WipLabels",
                columns: new[] { "Status", "CurrentSectionId", "CurrentOpNumber" });

            migrationBuilder.Sql(
                """
                UPDATE \"WipLabels\" AS l
                SET \"RootLabelId\" = l.\"Id\",
                    \"RootNumber\" = CASE
                        WHEN POSITION('/' IN l.\"Number\") > 0 THEN SPLIT_PART(l.\"Number\", '/', 1)
                        ELSE l.\"Number\"
                    END,
                    \"Suffix\" = CASE
                        WHEN POSITION('/' IN l.\"Number\") > 0
                            AND SPLIT_PART(l.\"Number\", '/', 2) ~ '^[0-9]+$'
                        THEN SPLIT_PART(l.\"Number\", '/', 2)::integer
                        ELSE 0
                    END,
                    \"Status\" = CASE
                        WHEN l.\"RemainingQuantity\" <= 0 THEN 'Consumed'
                        ELSE 'Active'
                    END;
                """);

            migrationBuilder.Sql(
                """
                UPDATE \"WipLabels\" AS l
                SET \"CurrentSectionId\" = r.\"SectionId\",
                    \"CurrentOpNumber\" = r.\"OpNumber\"
                FROM \"WipReceipts\" AS r
                WHERE r.\"WipLabelId\" = l.\"Id\";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WipLabels_RootLabelId",
                table: "WipLabels");

            migrationBuilder.DropIndex(
                name: "IX_WipLabels_Status_CurrentSectionId_CurrentOpNumber",
                table: "WipLabels");

            migrationBuilder.DropColumn(
                name: "CurrentOpNumber",
                table: "WipLabels");

            migrationBuilder.DropColumn(
                name: "CurrentSectionId",
                table: "WipLabels");

            migrationBuilder.DropColumn(
                name: "ParentLabelId",
                table: "WipLabels");

            migrationBuilder.DropColumn(
                name: "RootLabelId",
                table: "WipLabels");

            migrationBuilder.DropColumn(
                name: "RootNumber",
                table: "WipLabels");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "WipLabels");

            migrationBuilder.DropColumn(
                name: "Suffix",
                table: "WipLabels");
        }
    }
}
