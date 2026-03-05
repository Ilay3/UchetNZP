using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Configuration;

namespace UchetNZP.Web.Services;

public class WarehouseDailyResetService : BackgroundService
{
    private static readonly TimeZoneInfo ResetTimeZone = TimeZoneInfo.CreateCustomTimeZone(
        "WarehouseResetUtcPlus4",
        TimeSpan.FromHours(4),
        "UTC+04:00",
        "UTC+04:00");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<WarehouseDailyResetOptions> _optionsMonitor;
    private readonly ILogger<WarehouseDailyResetService> _logger;

    public WarehouseDailyResetService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<WarehouseDailyResetOptions> optionsMonitor,
        ILogger<WarehouseDailyResetService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var nextRun = GetNextRun(nowUtc);
            var delay = nextRun - nowUtc;

            _logger.LogInformation(
                "Следующее ежедневное обнуление склада запланировано на {NextRunLocal} (UTC+4).",
                nextRun);

            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (!_optionsMonitor.CurrentValue.Enabled)
            {
                _logger.LogInformation("Ежедневное обнуление склада отключено в настройках.");
                continue;
            }

            try
            {
                await ResetWarehouseAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при ежедневном обнулении склада.");
            }
        }
    }

    private static DateTimeOffset GetNextRun(DateTimeOffset nowUtc)
    {
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, ResetTimeZone);
        var localDate = localNow.Date;
        var localTarget = new DateTimeOffset(
            localDate.Year,
            localDate.Month,
            localDate.Day,
            18,
            0,
            0,
            ResetTimeZone.BaseUtcOffset);

        if (localNow >= localTarget)
        {
            localTarget = localTarget.AddDays(1);
        }

        return localTarget.ToUniversalTime();
    }

    private async Task ResetWarehouseAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var updatedAt = DateTime.UtcNow;

        var updatedLabelItems = await dbContext.WarehouseLabelItems
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Quantity, 0m)
                .SetProperty(item => item.UpdatedAt, updatedAt), cancellationToken)
            .ConfigureAwait(false);

        var updatedItems = await dbContext.WarehouseItems
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Quantity, 0m)
                .SetProperty(item => item.UpdatedAt, updatedAt), cancellationToken)
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Ежедневное обнуление склада выполнено в 18:00 UTC+4: обновлено записей склада {UpdatedItems}, ярлыков склада {UpdatedLabelItems}.",
            updatedItems,
            updatedLabelItems);
    }
}
