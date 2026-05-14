using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UchetNZP.Infrastructure.Migrations;

public partial class AddMetalOneCStructureFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "MetalSuppliers" ADD COLUMN IF NOT EXISTS "FullName" character varying(512);
            ALTER TABLE "MetalSuppliers" ADD COLUMN IF NOT EXISTS "Kpp" character varying(9);
            ALTER TABLE "MetalSuppliers" ADD COLUMN IF NOT EXISTS "LegalEntityKind" character varying(64) NOT NULL DEFAULT 'ЮридическоеЛицо';
            ALTER TABLE "MetalSuppliers" ADD COLUMN IF NOT EXISTS "CountryOfRegistration" character varying(128);
            ALTER TABLE "MetalSuppliers" ADD COLUMN IF NOT EXISTS "Okpo" character varying(16);
            ALTER TABLE "MetalSuppliers" ADD COLUMN IF NOT EXISTS "MainBankAccount" character varying(128);
            ALTER TABLE "MetalSuppliers" ADD COLUMN IF NOT EXISTS "MainContractName" character varying(256);
            ALTER TABLE "MetalSuppliers" ADD COLUMN IF NOT EXISTS "ContactPerson" character varying(256);
            ALTER TABLE "MetalSuppliers" ADD COLUMN IF NOT EXISTS "AdditionalInfo" character varying(1024);
            ALTER TABLE "MetalSuppliers" ADD COLUMN IF NOT EXISTS "Comment" character varying(512);

            ALTER TABLE "MetalMaterials" ADD COLUMN IF NOT EXISTS "FullName" character varying(512);
            ALTER TABLE "MetalMaterials" ADD COLUMN IF NOT EXISTS "Article" character varying(128);
            ALTER TABLE "MetalMaterials" ADD COLUMN IF NOT EXISTS "NomenclatureType" character varying(128);
            ALTER TABLE "MetalMaterials" ADD COLUMN IF NOT EXISTS "UnitOfMeasure" character varying(32) NOT NULL DEFAULT 'кг';
            ALTER TABLE "MetalMaterials" ADD COLUMN IF NOT EXISTS "NomenclatureGroup" character varying(128);
            ALTER TABLE "MetalMaterials" ADD COLUMN IF NOT EXISTS "VatRateType" character varying(64);
            ALTER TABLE "MetalMaterials" ADD COLUMN IF NOT EXISTS "CountryOfOrigin" character varying(128);
            ALTER TABLE "MetalMaterials" ADD COLUMN IF NOT EXISTS "CustomsDeclarationNumber" character varying(64);
            ALTER TABLE "MetalMaterials" ADD COLUMN IF NOT EXISTS "TnVedCode" character varying(32);
            ALTER TABLE "MetalMaterials" ADD COLUMN IF NOT EXISTS "Okpd2Code" character varying(32);
            ALTER TABLE "MetalMaterials" ADD COLUMN IF NOT EXISTS "Comment" character varying(1024);
            ALTER TABLE "MetalMaterials" ADD COLUMN IF NOT EXISTS "IsService" boolean NOT NULL DEFAULT FALSE;

            ALTER TABLE "MetalReceipts" ADD COLUMN IF NOT EXISTS "OrganizationName" character varying(256) NOT NULL DEFAULT 'НЗП';
            ALTER TABLE "MetalReceipts" ADD COLUMN IF NOT EXISTS "WarehouseName" character varying(128) NOT NULL DEFAULT 'Склад металла';
            ALTER TABLE "MetalReceipts" ADD COLUMN IF NOT EXISTS "OperationType" character varying(64) NOT NULL DEFAULT 'Поступление товаров';
            ALTER TABLE "MetalReceipts" ADD COLUMN IF NOT EXISTS "CurrencyCode" character varying(3) NOT NULL DEFAULT 'RUB';
            ALTER TABLE "MetalReceipts" ADD COLUMN IF NOT EXISTS "ContractName" character varying(256);
            ALTER TABLE "MetalReceipts" ADD COLUMN IF NOT EXISTS "SupplierDocumentDate" timestamp with time zone;
            ALTER TABLE "MetalReceipts" ADD COLUMN IF NOT EXISTS "SettlementAccount" character varying(16) NOT NULL DEFAULT '60.01';
            ALTER TABLE "MetalReceipts" ADD COLUMN IF NOT EXISTS "AdvanceAccount" character varying(16) NOT NULL DEFAULT '60.02';
            ALTER TABLE "MetalReceipts" ADD COLUMN IF NOT EXISTS "ResponsibleUserName" character varying(128);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "MetalReceipts" DROP COLUMN IF EXISTS "ResponsibleUserName";
            ALTER TABLE "MetalReceipts" DROP COLUMN IF EXISTS "AdvanceAccount";
            ALTER TABLE "MetalReceipts" DROP COLUMN IF EXISTS "SettlementAccount";
            ALTER TABLE "MetalReceipts" DROP COLUMN IF EXISTS "SupplierDocumentDate";
            ALTER TABLE "MetalReceipts" DROP COLUMN IF EXISTS "ContractName";
            ALTER TABLE "MetalReceipts" DROP COLUMN IF EXISTS "CurrencyCode";
            ALTER TABLE "MetalReceipts" DROP COLUMN IF EXISTS "OperationType";
            ALTER TABLE "MetalReceipts" DROP COLUMN IF EXISTS "WarehouseName";
            ALTER TABLE "MetalReceipts" DROP COLUMN IF EXISTS "OrganizationName";

            ALTER TABLE "MetalMaterials" DROP COLUMN IF EXISTS "IsService";
            ALTER TABLE "MetalMaterials" DROP COLUMN IF EXISTS "Comment";
            ALTER TABLE "MetalMaterials" DROP COLUMN IF EXISTS "Okpd2Code";
            ALTER TABLE "MetalMaterials" DROP COLUMN IF EXISTS "TnVedCode";
            ALTER TABLE "MetalMaterials" DROP COLUMN IF EXISTS "CustomsDeclarationNumber";
            ALTER TABLE "MetalMaterials" DROP COLUMN IF EXISTS "CountryOfOrigin";
            ALTER TABLE "MetalMaterials" DROP COLUMN IF EXISTS "VatRateType";
            ALTER TABLE "MetalMaterials" DROP COLUMN IF EXISTS "NomenclatureGroup";
            ALTER TABLE "MetalMaterials" DROP COLUMN IF EXISTS "UnitOfMeasure";
            ALTER TABLE "MetalMaterials" DROP COLUMN IF EXISTS "NomenclatureType";
            ALTER TABLE "MetalMaterials" DROP COLUMN IF EXISTS "Article";
            ALTER TABLE "MetalMaterials" DROP COLUMN IF EXISTS "FullName";

            ALTER TABLE "MetalSuppliers" DROP COLUMN IF EXISTS "Comment";
            ALTER TABLE "MetalSuppliers" DROP COLUMN IF EXISTS "AdditionalInfo";
            ALTER TABLE "MetalSuppliers" DROP COLUMN IF EXISTS "ContactPerson";
            ALTER TABLE "MetalSuppliers" DROP COLUMN IF EXISTS "MainContractName";
            ALTER TABLE "MetalSuppliers" DROP COLUMN IF EXISTS "MainBankAccount";
            ALTER TABLE "MetalSuppliers" DROP COLUMN IF EXISTS "Okpo";
            ALTER TABLE "MetalSuppliers" DROP COLUMN IF EXISTS "CountryOfRegistration";
            ALTER TABLE "MetalSuppliers" DROP COLUMN IF EXISTS "LegalEntityKind";
            ALTER TABLE "MetalSuppliers" DROP COLUMN IF EXISTS "Kpp";
            ALTER TABLE "MetalSuppliers" DROP COLUMN IF EXISTS "FullName";
            """);
    }
}
