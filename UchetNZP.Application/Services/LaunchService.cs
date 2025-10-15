using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Launches;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Application.Services;

public class LaunchService : ILaunchService
{
    private readonly AppDbContext _dbContext;
    private readonly IRouteService _routeService;

    public LaunchService(AppDbContext dbContext, IRouteService routeService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
    }

    public async Task<LaunchBatchSummaryDto> AddLaunchesBatchAsync(IEnumerable<LaunchItemDto> items, CancellationToken cancellationToken = default)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        var materialized = items.ToList();
        if (materialized.Count == 0)
        {
            return new LaunchBatchSummaryDto(0, Array.Empty<LaunchItemSummaryDto>());
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var results = new List<LaunchItemSummaryDto>(materialized.Count);

            foreach (var item in materialized)
            {
                if (item.Quantity <= 0)
                {
                    throw new InvalidOperationException($"Количество запуска должно быть больше нуля для детали {item.PartId}.");
                }

                var routeStart = await _dbContext.PartRoutes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.PartId == item.PartId && x.OpNumber == item.FromOpNumber, cancellationToken)
                    .ConfigureAwait(false);

                if (routeStart is null)
                {
                    throw new InvalidOperationException($"Операция {item.FromOpNumber} для детали {item.PartId} не найдена в маршруте.");
                }

                var balance = await _dbContext.WipBalances
                    .FirstOrDefaultAsync(
                        x => x.PartId == item.PartId && x.SectionId == routeStart.SectionId && x.OpNumber == item.FromOpNumber,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (balance is null)
                {
                    throw new InvalidOperationException($"Остаток НЗП по детали {item.PartId} и операции {item.FromOpNumber} отсутствует.");
                }

                if (item.Quantity > balance.Quantity)
                {
                    throw new InvalidOperationException($"Недостаточно остатка НЗП по детали {item.PartId} на операции {item.FromOpNumber}. Доступно {balance.Quantity}, требуется {item.Quantity}.");
                }

                var tail = await _routeService.GetTailToFinishAsync(
                        item.PartId,
                        item.FromOpNumber.ToString(CultureInfo.InvariantCulture),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (tail.Count == 0)
                {
                    throw new InvalidOperationException($"Хвост маршрута для детали {item.PartId} и операции {item.FromOpNumber} пуст.");
                }

                var sumNormHours = tail.Sum(x => x.NormHours);
                var sumHours = item.Quantity * sumNormHours;

                var launch = new WipLaunch
                {
                    Id = Guid.NewGuid(),
                    PartId = item.PartId,
                    SectionId = routeStart.SectionId,
                    LaunchDate = item.LaunchDate,
                    Quantity = item.Quantity,
                    DocumentNumber = item.DocumentNumber,
                    SumHoursToFinish = sumHours,
                };

                await _dbContext.WipLaunches.AddAsync(launch, cancellationToken).ConfigureAwait(false);

                foreach (var operation in tail)
                {
                    var launchOperation = new WipLaunchOperation
                    {
                        Id = Guid.NewGuid(),
                        WipLaunchId = launch.Id,
                        OperationId = operation.OperationId,
                        SectionId = operation.SectionId,
                        OpNumber = operation.OpNumber,
                        PartRouteId = operation.Id,
                        Quantity = item.Quantity,
                        Hours = operation.NormHours * item.Quantity,
                    };

                    await _dbContext.WipLaunchOperations.AddAsync(launchOperation, cancellationToken).ConfigureAwait(false);
                }

                balance.Quantity -= item.Quantity;

                results.Add(new LaunchItemSummaryDto(
                    item.PartId,
                    item.FromOpNumber,
                    routeStart.SectionId,
                    item.Quantity,
                    balance.Quantity,
                    sumHours,
                    launch.Id));
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new LaunchBatchSummaryDto(results.Count, results);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
