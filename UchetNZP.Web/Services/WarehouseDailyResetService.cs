using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Configuration;

namespace UchetNZP.Web.Services;

public class WarehouseDailyResetService : BackgroundService
{
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
            var now = DateTime.Now;
            var nextRun = now.Date.AddDays(1);
            var delay = nextRun - now;

            _logger.LogInformation("Следующее ежедневное обнуление склада запланировано на {NextRun}.", nextRun);

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

    private async Task ResetWarehouseAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var deletedLabelItems = await dbContext.WarehouseLabelItems
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var deletedItems = await dbContext.WarehouseItems
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Ежедневное обнуление склада выполнено: удалено записей склада {DeletedItems}, ярлыков склада {DeletedLabelItems}.",
            deletedItems,
            deletedLabelItems);
    }
}
