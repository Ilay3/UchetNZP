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
builder.Services.AddSingleton<IScrapReportPdfExporter, ScrapReportPdfExporter>();
builder.Services.AddSingleton<ITransferPeriodReportPdfExporter, TransferPeriodReportPdfExporter>();
builder.Services.AddSingleton<IWipBatchReportPdfExporter, WipBatchReportPdfExporter>();
builder.Services.AddSingleton<IReceiptReportPdfExporter, ReceiptReportPdfExporter>();
builder.Services.AddSingleton<IWipBatchInventoryDocumentExporter, WipBatchInventoryDocumentExporter>();
builder.Services.AddScoped<IWipEscortLabelDocumentService, WipEscortLabelDocumentService>();
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
        await EnsureMetalMaterialsSeededAsync(db, CancellationToken.None);
        await EnsureMetalSuppliersSeededAsync(db, CancellationToken.None);
        await EnsureMetalReceiptParametersSeededAsync(db, CancellationToken.None);
        await EnsureMetalReceiptFinanceColumnsAsync(db, CancellationToken.None);
        await EnsureMetalConsumptionNormsSeededAsync(db, CancellationToken.None);
    }
    else
    {
        await db.Database.EnsureCreatedAsync(CancellationToken.None);
        await EnsureMetalMaterialsSeededAsync(db, CancellationToken.None);
        await EnsureMetalSuppliersSeededAsync(db, CancellationToken.None);
        await EnsureMetalReceiptParametersSeededAsync(db, CancellationToken.None);
        await EnsureMetalReceiptFinanceColumnsAsync(db, CancellationToken.None);
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
            ADD COLUMN IF NOT EXISTS "VatAccount" character varying(16) NOT NULL DEFAULT '19.01';

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
