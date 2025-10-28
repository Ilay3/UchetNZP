using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDuplicatePartCodeSuffix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
UPDATE "Parts"
SET "Name" = trim(trailing FROM left("Name", char_length("Name") - char_length("Code") - 3))
WHERE "Code" IS NOT NULL
  AND char_length("Name") > char_length("Code") + 3
  AND "Name" LIKE '% (' || "Code" || ')';
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
