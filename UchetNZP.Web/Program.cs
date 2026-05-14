using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Services;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Configuration;
using QuestPDF.Infrastructure;
using UchetNZP.Web.Services;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

builder.Services.AddControllersWithViews(options =>
{
    options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(_ => "Поле обязательно для заполнения.");
    options.ModelBindingMessageProvider.SetValueIsInvalidAccessor(value => $"Значение '{value}' имеет неверный формат. Используйте число в формате 123,59.");
    options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(_ => "Введите число в формате 123,59.");
});

var ruCulture = CultureInfo.GetCultureInfo("ru-RU");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(ruCulture);
    options.SupportedCultures = new[] { ruCulture };
    options.SupportedUICultures = new[] { ruCulture };
});
builder.Host.UseWindowsService();

builder.Services.Configure<MaintenanceOptions>(builder.Configuration.GetSection("Maintenance"));
builder.Services.Configure<BackgroundBubblesOptions>(builder.Configuration.GetSection("BackgroundBubbles"));
builder.Services.Configure<WarehouseDailyResetOptions>(builder.Configuration.GetSection("WarehouseDailyReset"));
builder.Services.Configure<TransferOptions>(builder.Configuration.GetSection("Transfer"));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = "Dummy";
        options.DefaultChallengeScheme = "Dummy";
        options.DefaultForbidScheme = "Dummy";
    })
    .AddScheme<AuthenticationSchemeOptions, DummyAuthenticationHandler>("Dummy", _ => { });

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IRouteService, RouteService>();
builder.Services.AddScoped<IWipService, WipService>();
builder.Services.AddScoped<ILaunchService, LaunchService>();
builder.Services.AddScoped<IMaterialSelectionService, MaterialSelectionService>();
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<ILabelNumberingService, LabelNumberingService>();
builder.Services.AddScoped<IImportService, ImportService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAdminWipService, AdminWipService>();
builder.Services.AddScoped<IAdminCatalogService, AdminCatalogService>();
builder.Services.AddScoped<IWipLabelService, WipLabelService>();
builder.Services.AddScoped<ICuttingPlanService, CuttingPlanService>();
builder.Services.AddScoped<IWipLabelLookupService, WipLabelLookupService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IScrapReportExcelExporter, ScrapReportExcelExporter>();
builder.Services.AddSingleton<ITransferPeriodReportExcelExporter, TransferPeriodReportExcelExporter>();
builder.Services.AddSingleton<IWipBatchReportExcelExporter, WipBatchReportExcelExporter>();
builder.Services.AddSingleton<IFullMovementReportExcelExporter, FullMovementReportExcelExporter>();
builder.Services.AddSingleton<IScrapReportPdfExporter, ScrapReportPdfExporter>();
builder.Services.AddSingleton<ITransferPeriodReportPdfExporter, TransferPeriodReportPdfExporter>();
builder.Services.AddSingleton<IWipBatchReportPdfExporter, WipBatchReportPdfExporter>();
builder.Services.AddSingleton<IReceiptReportPdfExporter, ReceiptReportPdfExporter>();
builder.Services.AddSingleton<IWipBatchInventoryDocumentExporter, WipBatchInventoryDocumentExporter>();
builder.Services.AddSingleton<IWordToPdfConverter, LibreOfficeWordToPdfConverter>();
builder.Services.AddScoped<IWipEscortLabelDocumentService, WipEscortLabelDocumentService>();
builder.Services.AddScoped<IWarehouseControlCardDocumentService, WarehouseControlCardDocumentService>();
builder.Services.AddScoped<IMetalRequirementWarehousePrintDocumentService, MetalRequirementWarehousePrintDocumentService>();
builder.Services.AddScoped<IMetalReceiptItemLabelDocumentService, MetalReceiptItemLabelDocumentService>();
builder.Services.AddScoped<IMetalReceiptDocumentService, MetalReceiptDocumentService>();
builder.Services.AddSingleton<IWipHistoryExcelExporter, WipHistoryExcelExporter>();
builder.Services.AddSingleton<ICuttingMapExcelExporter, CuttingMapExcelExporter>();
builder.Services.AddSingleton<ICuttingMapPdfExporter, CuttingMapPdfExporter>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
        await EnsureTransferAuditResidualColumnsAsync(db, CancellationToken.None);
        await EnsureMetalReceiptItemConsumptionColumnsAsync(db, CancellationToken.None);
        await RouteOperationNameSynchronizer.EnsureOperationNamesMatchSectionsAsync(db, CancellationToken.None);
        await EnsureMetalOneCStructureColumnsAsync(db, CancellationToken.None);
        await EnsureMetalReceiptFinanceColumnsAsync(db, CancellationToken.None);
        await EnsureWarehouseFinishedGoodsColumnsAsync(db, CancellationToken.None);
        await EnsureMetalMaterialsSeededAsync(db, CancellationToken.None);
        await EnsureMetalSuppliersSeededAsync(db, CancellationToken.None);
        await EnsureMetalReceiptParametersSeededAsync(db, CancellationToken.None);
        await EnsureMetalConsumptionNormsSeededAsync(db, CancellationToken.None);
    }
    else
    {
        await db.Database.EnsureCreatedAsync(CancellationToken.None);
        await EnsureMetalOneCStructureColumnsAsync(db, CancellationToken.None);
        await EnsureMetalReceiptFinanceColumnsAsync(db, CancellationToken.None);
        await EnsureWarehouseFinishedGoodsColumnsAsync(db, CancellationToken.None);
        await EnsureMetalMaterialsSeededAsync(db, CancellationToken.None);
        await EnsureMetalSuppliersSeededAsync(db, CancellationToken.None);
        await EnsureMetalReceiptParametersSeededAsync(db, CancellationToken.None);
        await EnsureMetalConsumptionNormsSeededAsync(db, CancellationToken.None);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRequestLocalization();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static async Task EnsureTransferAuditResidualColumnsAsync(AppDbContext in_db, CancellationToken in_cancellationToken)
{
    await in_db.Database.ExecuteSqlRawAsync(
        """
        ALTER TABLE "TransferAudits"
            ADD COLUMN IF NOT EXISTS "ResidualWipLabelId" uuid;

        ALTER TABLE "TransferAudits"
            ADD COLUMN IF NOT EXISTS "ResidualLabelQuantity" numeric;

        ALTER TABLE "TransferAudits"
            ADD COLUMN IF NOT EXISTS "ResidualLabelNumber" character varying(64);
        """,
        in_cancellationToken);
}

static async Task EnsureMetalReceiptItemConsumptionColumnsAsync(AppDbContext in_db, CancellationToken in_cancellationToken)
{
    await in_db.Database.ExecuteSqlRawAsync(
        """
        ALTER TABLE "MetalReceiptItems"
            ADD COLUMN IF NOT EXISTS "ConsumedAt" timestamp with time zone;

        ALTER TABLE "MetalReceiptItems"
            ADD COLUMN IF NOT EXISTS "ConsumedByCuttingReportId" uuid;

        ALTER TABLE "MetalReceiptItems"
            ADD COLUMN IF NOT EXISTS "IsConsumed" boolean NOT NULL DEFAULT FALSE;

        CREATE INDEX IF NOT EXISTS "IX_MetalReceiptItems_IsConsumed"
            ON "MetalReceiptItems" ("IsConsumed");
        """,
        in_cancellationToken);
}


static async Task EnsureMetalReceiptFinanceColumnsAsync(AppDbContext in_db, CancellationToken in_cancellationToken)
{
    await in_db.Database.ExecuteSqlRawAsync(
        """
        ALTER TABLE "MetalReceipts"
            ADD COLUMN IF NOT EXISTS "AccountingAccount" character varying(16) NOT NULL DEFAULT '10.01';

        ALTER TABLE "MetalReceipts"
            ADD COLUMN IF NOT EXISTS "VatAccount" character varying(16) NOT NULL DEFAULT '19.03';

        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'MetalReceiptItems'
                  AND column_name = 'priceperkg'
            )
            AND NOT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'MetalReceiptItems'
                  AND column_name = 'PricePerKg'
            ) THEN
                EXECUTE 'ALTER TABLE "MetalReceiptItems" RENAME COLUMN priceperkg TO "PricePerKg"';
            END IF;

            IF NOT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'MetalReceiptItems'
                  AND column_name = 'PricePerKg'
            ) THEN
                EXECUTE 'ALTER TABLE "MetalReceiptItems" ADD COLUMN "PricePerKg" numeric(18,4) NOT NULL DEFAULT 0';
            END IF;
        END
        $$;
        """,


        in_cancellationToken);
}

static async Task EnsureMetalOneCStructureColumnsAsync(AppDbContext in_db, CancellationToken in_cancellationToken)
{
    await in_db.Database.ExecuteSqlRawAsync(
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
        """,
        in_cancellationToken);
}

static async Task EnsureWarehouseFinishedGoodsColumnsAsync(AppDbContext in_db, CancellationToken in_cancellationToken)
{
    await in_db.Database.ExecuteSqlRawAsync(
        """
        CREATE TABLE IF NOT EXISTS "WarehouseAssemblyUnits" (
            "Id" uuid NOT NULL,
            "Name" character varying(256) NOT NULL,
            "NormalizedName" character varying(256) NOT NULL,
            "CreatedByUserId" uuid,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_WarehouseAssemblyUnits" PRIMARY KEY ("Id")
        );

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_WarehouseAssemblyUnits_NormalizedName"
            ON "WarehouseAssemblyUnits" ("NormalizedName");

        ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "MovementType" character varying(32) NOT NULL DEFAULT 'Receipt';
        ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "SourceType" character varying(32) NOT NULL DEFAULT 'AutomaticTransfer';
        ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "DocumentNumber" character varying(64);
        ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "ControlCardNumber" character varying(64);
        ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "ControllerName" character varying(128);
        ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "MasterName" character varying(128);
        ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "AcceptedByName" character varying(128);
        ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "CreatedByUserId" uuid;
        ALTER TABLE "WarehouseItems" ADD COLUMN IF NOT EXISTS "AssemblyUnitId" uuid;
        ALTER TABLE "WarehouseLabelItems" ADD COLUMN IF NOT EXISTS "LabelNumber" character varying(32);
        ALTER TABLE "WarehouseLabelItems" ALTER COLUMN "WipLabelId" DROP NOT NULL;

        UPDATE "WarehouseLabelItems" AS item
        SET "LabelNumber" = label."Number"
        FROM "WipLabels" AS label
        WHERE item."WipLabelId" = label."Id"
          AND (item."LabelNumber" IS NULL OR item."LabelNumber" = '');

        ALTER TABLE "WarehouseItems" ALTER COLUMN "PartId" DROP NOT NULL;

        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conname = 'FK_WarehouseItems_Parts_PartId'
                  AND conrelid = '"WarehouseItems"'::regclass
                  AND confdeltype <> 'n'
            ) THEN
                ALTER TABLE "WarehouseItems" DROP CONSTRAINT "FK_WarehouseItems_Parts_PartId";
            END IF;

            IF NOT EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conname = 'FK_WarehouseItems_Parts_PartId'
                  AND conrelid = '"WarehouseItems"'::regclass
            ) THEN
                ALTER TABLE "WarehouseItems"
                    ADD CONSTRAINT "FK_WarehouseItems_Parts_PartId"
                    FOREIGN KEY ("PartId") REFERENCES "Parts" ("Id") ON DELETE SET NULL;
            END IF;
        END
        $$;

        CREATE INDEX IF NOT EXISTS "IX_WarehouseItems_MovementType"
            ON "WarehouseItems" ("MovementType");

        CREATE INDEX IF NOT EXISTS "IX_WarehouseItems_SourceType"
            ON "WarehouseItems" ("SourceType");

        CREATE INDEX IF NOT EXISTS "IX_WarehouseItems_AssemblyUnitId"
            ON "WarehouseItems" ("AssemblyUnitId");

        CREATE INDEX IF NOT EXISTS "IX_WarehouseLabelItems_LabelNumber"
            ON "WarehouseLabelItems" ("LabelNumber");

        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conname = 'FK_WarehouseLabelItems_WipLabels_WipLabelId'
                  AND conrelid = '"WarehouseLabelItems"'::regclass
                  AND confdeltype <> 'n'
            ) THEN
                ALTER TABLE "WarehouseLabelItems" DROP CONSTRAINT "FK_WarehouseLabelItems_WipLabels_WipLabelId";
            END IF;

            IF NOT EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conname = 'FK_WarehouseLabelItems_WipLabels_WipLabelId'
                  AND conrelid = '"WarehouseLabelItems"'::regclass
            ) THEN
                ALTER TABLE "WarehouseLabelItems"
                    ADD CONSTRAINT "FK_WarehouseLabelItems_WipLabels_WipLabelId"
                    FOREIGN KEY ("WipLabelId") REFERENCES "WipLabels" ("Id") ON DELETE SET NULL;
            END IF;
        END
        $$;

        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conname = 'FK_WarehouseItems_WarehouseAssemblyUnits_AssemblyUnitId'
                  AND conrelid = '"WarehouseItems"'::regclass
            ) THEN
                ALTER TABLE "WarehouseItems"
                    ADD CONSTRAINT "FK_WarehouseItems_WarehouseAssemblyUnits_AssemblyUnitId"
                    FOREIGN KEY ("AssemblyUnitId") REFERENCES "WarehouseAssemblyUnits" ("Id") ON DELETE SET NULL;
            END IF;
        END
        $$;
        """,
        in_cancellationToken);
}

static async Task EnsureMetalMaterialsSeededAsync(AppDbContext in_db, CancellationToken in_cancellationToken)
{
    if (await in_db.MetalMaterials.AnyAsync(in_cancellationToken))
    {
        return;
    }

    in_db.MetalMaterials.AddRange(
        new UchetNZP.Domain.Entities.MetalMaterial
        {
            Id = Guid.NewGuid(),
            Name = "Лист ст.35 t=6 ГОСТ19903-74/1577-93",
            Code = "LIST35T6",
            UnitKind = "SquareMeter",
            MassPerSquareMeterKg = 1.5m,
            CoefConsumption = 1m,
            StockUnit = "m2",
            WeightPerUnitKg = 1.5m,
            Coefficient = 1m,
            DisplayName = "Лист ст.35 t=6",
            IsActive = true,
        },
        new UchetNZP.Domain.Entities.MetalMaterial
        {
            Id = Guid.NewGuid(),
            Name = "Пруток 20Г",
            Code = "PRUT20G",
            UnitKind = "Meter",
            MassPerMeterKg = 0.8m,
            CoefConsumption = 1m,
            StockUnit = "m",
            WeightPerUnitKg = 0.8m,
            Coefficient = 1m,
            DisplayName = "Пруток 20Г",
            IsActive = true,
        },
        new UchetNZP.Domain.Entities.MetalMaterial
        {
            Id = Guid.NewGuid(),
            Name = "Круг 45",
            Code = "KRUG45",
            UnitKind = "Meter",
            MassPerMeterKg = 0.9m,
            CoefConsumption = 1m,
            StockUnit = "m",
            WeightPerUnitKg = 0.9m,
            Coefficient = 1m,
            DisplayName = "Круг 45",
            IsActive = true,
        },
        new UchetNZP.Domain.Entities.MetalMaterial
        {
            Id = Guid.NewGuid(),
            Name = "Лист 09Г2С t=4",
            Code = "LIST09G2S4",
            UnitKind = "SquareMeter",
            MassPerSquareMeterKg = 1.2m,
            CoefConsumption = 1m,
            StockUnit = "m2",
            WeightPerUnitKg = 1.2m,
            Coefficient = 1m,
            DisplayName = "Лист 09Г2С t=4",
            IsActive = true,
        });

    await in_db.SaveChangesAsync(in_cancellationToken);
}

static async Task EnsureMetalSuppliersSeededAsync(AppDbContext in_db, CancellationToken in_cancellationToken)
{
    if (await in_db.MetalSuppliers.AnyAsync(x => x.IsActive, in_cancellationToken))
    {
        return;
    }

    in_db.MetalSuppliers.Add(new UchetNZP.Domain.Entities.MetalSupplier
    {
        Id = Guid.NewGuid(),
        Identifier = "00-001828",
        Name = "АО \"Металлоторг\"",
        Inn = "1234567890",
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    });

    await in_db.SaveChangesAsync(in_cancellationToken);
}

static async Task EnsureMetalReceiptParametersSeededAsync(AppDbContext in_db, CancellationToken in_cancellationToken)
{
    const string vatRateKey = "MetalReceipt.VatRatePercent";
    if (await in_db.SystemParameters.AnyAsync(x => x.Key == vatRateKey, in_cancellationToken))
    {
        return;
    }

    in_db.SystemParameters.Add(new UchetNZP.Domain.Entities.SystemParameter
    {
        Key = vatRateKey,
        DecimalValue = 22m,
        Description = "Ставка НДС для прихода металла, %",
        UpdatedAt = DateTime.UtcNow,
    });

    await in_db.SaveChangesAsync(in_cancellationToken);
}

static async Task EnsureMetalConsumptionNormsSeededAsync(AppDbContext in_db, CancellationToken in_cancellationToken)
{
    if (await in_db.MetalConsumptionNorms.AnyAsync(x => x.IsActive, in_cancellationToken))
    {
        return;
    }

    var parts = await in_db.Parts
        .AsNoTracking()
        .OrderBy(x => x.Name)
        .Take(2)
        .ToListAsync(in_cancellationToken);

    if (parts.Count == 0)
    {
        return;
    }

    var norms = new List<UchetNZP.Domain.Entities.MetalConsumptionNorm>();

    foreach (var part in parts)
    {
        norms.Add(new UchetNZP.Domain.Entities.MetalConsumptionNorm
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            BaseConsumptionQty = 0.25m,
            ConsumptionUnit = "м",
            SizeRaw = "1000x500",
            SourceFile = "seed",
            Comment = "Тестовая норма для демонстрации UI.",
            IsActive = true,
        });
    }

    in_db.MetalConsumptionNorms.AddRange(norms);
    await in_db.SaveChangesAsync(in_cancellationToken);
}

public partial class Program
{
}
