using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLabelNumberCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabelNumberCounters",
                columns: table => new
                {
                    RootNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NextSuffix = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabelNumberCounters", x => x.RootNumber);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "LabelNumberCounters" ("RootNumber", "NextSuffix")
                SELECT l."RootNumber", GREATEST(MAX(l."Suffix") + 1, 1)
                FROM "WipLabels" AS l
                WHERE l."RootNumber" IS NOT NULL AND l."RootNumber" <> ''
                GROUP BY l."RootNumber";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabelNumberCounters");
        }
    }
}
