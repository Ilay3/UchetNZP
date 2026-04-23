using System.Threading;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Services;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Configuration;
using QuestPDF.Infrastructure;
using UchetNZP.Web.Services;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

builder.Services.AddControllersWithViews();
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
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<ILabelNumberingService, LabelNumberingService>();
builder.Services.AddScoped<IImportService, ImportService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAdminWipService, AdminWipService>();
builder.Services.AddScoped<IAdminCatalogService, AdminCatalogService>();
builder.Services.AddScoped<IWipLabelService, WipLabelService>();
builder.Services.AddScoped<IWipLabelLookupService, WipLabelLookupService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IScrapReportExcelExporter, ScrapReportExcelExporter>();
builder.Services.AddSingleton<ITransferPeriodReportExcelExporter, TransferPeriodReportExcelExporter>();
builder.Services.AddSingleton<IWipBatchReportExcelExporter, WipBatchReportExcelExporter>();
builder.Services.AddSingleton<IScrapReportPdfExporter, ScrapReportPdfExporter>();
builder.Services.AddSingleton<ITransferPeriodReportPdfExporter, TransferPeriodReportPdfExporter>();
builder.Services.AddSingleton<IWipBatchReportPdfExporter, WipBatchReportPdfExporter>();
builder.Services.AddSingleton<IWipBatchInventoryDocumentExporter, WipBatchInventoryDocumentExporter>();
builder.Services.AddSingleton<IWipHistoryExcelExporter, WipHistoryExcelExporter>();
builder.Services.AddHostedService<WarehouseDailyResetService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
        await EnsureTransferAuditResidualColumnsAsync(db, CancellationToken.None);
        await RouteOperationNameSynchronizer.EnsureOperationNamesMatchSectionsAsync(db, CancellationToken.None);
        await EnsureMetalMaterialsSeededAsync(db, CancellationToken.None);
    }
    else
    {
        await db.Database.EnsureCreatedAsync(CancellationToken.None);
        await EnsureMetalMaterialsSeededAsync(db, CancellationToken.None);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

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
            IsActive = true,
        },
        new UchetNZP.Domain.Entities.MetalMaterial
        {
            Id = Guid.NewGuid(),
            Name = "Пруток 20Г",
            Code = "PRUT20G",
            UnitKind = "Meter",
            IsActive = true,
        },
        new UchetNZP.Domain.Entities.MetalMaterial
        {
            Id = Guid.NewGuid(),
            Name = "Круг 45",
            Code = "KRUG45",
            UnitKind = "Meter",
            IsActive = true,
        },
        new UchetNZP.Domain.Entities.MetalMaterial
        {
            Id = Guid.NewGuid(),
            Name = "Лист 09Г2С t=4",
            Code = "LIST09G2S4",
            UnitKind = "SquareMeter",
            IsActive = true,
        });

    await in_db.SaveChangesAsync(in_cancellationToken);
}

public partial class Program
{
}
