using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations
{
    public partial class AddCuttingReports : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: CuttingReports and MetalReceiptItems consumption columns
            // were already introduced by 20260424110421_AddElectronicMetalRequirementsDocument.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: schema rollback is handled by
            // 20260424110421_AddElectronicMetalRequirementsDocument.
        }
    }
}
