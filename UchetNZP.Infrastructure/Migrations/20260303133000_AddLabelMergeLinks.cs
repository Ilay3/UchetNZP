using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations;

public partial class AddLabelMergeLinks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LabelMerges",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InputLabelId = table.Column<Guid>(type: "uuid", nullable: false),
                OutputLabelId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LabelMerges", x => x.Id);
                table.ForeignKey(
                    name: "FK_LabelMerges_WipLabels_InputLabelId",
                    column: x => x.InputLabelId,
                    principalTable: "WipLabels",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LabelMerges_WipLabels_OutputLabelId",
                    column: x => x.OutputLabelId,
                    principalTable: "WipLabels",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LabelMerges_InputLabelId",
            table: "LabelMerges",
            column: "InputLabelId");

        migrationBuilder.CreateIndex(
            name: "IX_LabelMerges_OutputLabelId",
            table: "LabelMerges",
            column: "OutputLabelId");

        migrationBuilder.CreateIndex(
            name: "IX_LabelMerges_InputLabelId_OutputLabelId",
            table: "LabelMerges",
            columns: new[] { "InputLabelId", "OutputLabelId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "LabelMerges");
    }
}
