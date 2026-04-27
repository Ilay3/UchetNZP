using System.Collections.Generic;
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
    private const string RequirementStatusCreated = "Created";
    private const string RequirementStatusUpdated = "Updated";

    private readonly AppDbContext _dbContext;
    private readonly IRouteService _routeService;
    private readonly ICurrentUserService _currentUserService;

    public LaunchService(AppDbContext dbContext, IRouteService routeService, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
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
            var pendingSummaries = new List<(Guid PartId, int FromOpNumber, Guid SectionId, decimal Quantity, decimal SumHoursToFinish, Guid LaunchId, WipBalance Balance)>(materialized.Count);

            foreach (var item in materialized)
            {
                var userId = _currentUserService.UserId;
                var now = DateTime.UtcNow;
                var launchDate = NormalizeToUtc(item.LaunchDate);

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
                    UserId = userId,
                    PartId = item.PartId,
                    SectionId = routeStart.SectionId,
                    FromOpNumber = item.FromOpNumber,
                    LaunchDate = launchDate,
                    CreatedAt = now,
                    Quantity = item.Quantity,
                    Comment = item.Comment,
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
                        NormHours = operation.NormHours,
                    };

                    await _dbContext.WipLaunchOperations.AddAsync(launchOperation, cancellationToken).ConfigureAwait(false);
                }

                await UpsertElectronicMetalRequirementAsync(
                        launch,
                        userId,
                        cancellationToken)
                    .ConfigureAwait(false);

                pendingSummaries.Add((
                    item.PartId,
                    item.FromOpNumber,
                    routeStart.SectionId,
                    item.Quantity,
                    sumHours,
                    launch.Id,
                    balance));
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            foreach (var pendingSummary in pendingSummaries)
            {
                await _dbContext.Entry(pendingSummary.Balance).ReloadAsync(cancellationToken).ConfigureAwait(false);
            }

            var results = pendingSummaries
                .Select(pendingSummary => new LaunchItemSummaryDto(
                    pendingSummary.PartId,
                    pendingSummary.FromOpNumber,
                    pendingSummary.SectionId,
                    pendingSummary.Quantity,
                    pendingSummary.Balance.Quantity,
                    pendingSummary.SumHoursToFinish,
                    pendingSummary.LaunchId))
                .ToList();
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new LaunchBatchSummaryDto(results.Count, results);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<LaunchDeleteResultDto> DeleteLaunchAsync(Guid launchId, CancellationToken cancellationToken = default)
    {
        if (launchId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор запуска не может быть пустым.", nameof(launchId));
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var launch = await _dbContext.WipLaunches
                .Include(x => x.Operations)
                .FirstOrDefaultAsync(x => x.Id == launchId, cancellationToken)
                .ConfigureAwait(false);

            if (launch is null)
            {
                throw new KeyNotFoundException($"Запуск {launchId} не найден.");
            }

            var balance = await _dbContext.WipBalances
                .FirstOrDefaultAsync(
                    x => x.PartId == launch.PartId &&
                         x.SectionId == launch.SectionId &&
                         x.OpNumber == launch.FromOpNumber,
                    cancellationToken)
                .ConfigureAwait(false);

            if (balance is null)
            {
                throw new InvalidOperationException($"Остаток НЗП по детали {launch.PartId} и операции {launch.FromOpNumber} отсутствует.");
            }

            if (launch.Operations.Count > 0)
            {
                _dbContext.WipLaunchOperations.RemoveRange(launch.Operations);
            }

            _dbContext.WipLaunches.Remove(launch);

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new LaunchDeleteResultDto(launch.Id, launch.PartId, launch.SectionId, launch.FromOpNumber, balance.Quantity);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }

    private async Task UpsertElectronicMetalRequirementAsync(WipLaunch launch, Guid userId, CancellationToken cancellationToken)
    {
        var activeNorms = await _dbContext.MetalConsumptionNorms
            .AsNoTracking()
            .Where(x => x.IsActive && x.PartId == launch.PartId && x.MetalMaterialId.HasValue && x.MetalMaterialId.Value != Guid.Empty)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (activeNorms.Count == 0)
        {
            return;
        }

        var materialIds = activeNorms
            .Select(x => x.MetalMaterialId!.Value)
            .Distinct()
            .ToList();

        var activeMaterials = await _dbContext.MetalMaterials
            .AsNoTracking()
            .Where(x => x.IsActive && materialIds.Contains(x.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (activeMaterials.Count == 0)
        {
            return;
        }

        var selectedNorm = activeNorms.FirstOrDefault(x => activeMaterials.Any(m => m.Id == x.MetalMaterialId!.Value));
        if (selectedNorm is null)
        {
            return;
        }

        var selectedMaterial = activeMaterials.First(x => x.Id == selectedNorm.MetalMaterialId!.Value);
        var calculation = MetalConsumptionCalculator.Calculate(selectedNorm, launch.Quantity, selectedMaterial);
        var requiredQty = selectedNorm.BaseConsumptionQty * launch.Quantity;
        var now = DateTime.UtcNow;
        var actor = userId == Guid.Empty ? "system" : userId.ToString();

        var requirement = await _dbContext.MetalRequirements
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.WipLaunchId == launch.Id, cancellationToken)
            .ConfigureAwait(false);

        if (requirement is null)
        {
            requirement = new MetalRequirement
            {
                Id = Guid.NewGuid(),
                RequirementNumber = await GetNextRequirementNumberAsync(cancellationToken).ConfigureAwait(false),
                RequirementDate = now,
                WipLaunchId = launch.Id,
                PartId = launch.PartId,
                PartCode = launch.Part?.Code ?? string.Empty,
                PartName = launch.Part?.Name ?? string.Empty,
                Quantity = launch.Quantity,
                MetalMaterialId = selectedMaterial.Id,
                Status = RequirementStatusCreated,
                Comment = "Документ создан в электронном виде автоматически при сохранении запуска партии",
                CreatedAt = now,
                CreatedBy = actor,
                UpdatedAt = now,
                UpdatedBy = actor,
            };

            requirement.Items.Add(new MetalRequirementItem
            {
                Id = Guid.NewGuid(),
                MetalMaterialId = selectedMaterial.Id,
                ConsumptionPerUnit = selectedNorm.BaseConsumptionQty,
                ConsumptionUnit = selectedNorm.ConsumptionUnit,
                RequiredQty = requiredQty,
                RequiredWeightKg = calculation.NeedKg,
                SizeRaw = selectedNorm.SizeRaw,
                Comment = "Строка сформирована автоматически из нормы расхода.",
                NormPerUnit = selectedNorm.BaseConsumptionQty,
                TotalRequiredQty = requiredQty,
                Unit = selectedNorm.ConsumptionUnit,
                TotalRequiredWeightKg = calculation.NeedKg,
                CalculationFormula = calculation.Formula,
                CalculationInput = calculation.FormulaInput,
                SelectionSource = "auto_from_launch",
                SelectionReason = "Автоматическое создание из запуска партии.",
                CandidateMaterials = null,
            });

            _dbContext.MetalRequirements.Add(requirement);
            return;
        }

        requirement.RequirementDate = now;
        requirement.PartId = launch.PartId;
        requirement.PartCode = launch.Part?.Code ?? requirement.PartCode;
        requirement.PartName = launch.Part?.Name ?? requirement.PartName;
        requirement.Quantity = launch.Quantity;
        requirement.MetalMaterialId = selectedMaterial.Id;
        requirement.Comment = "Документ создан в электронном виде автоматически при сохранении запуска партии";
        requirement.UpdatedAt = now;
        requirement.UpdatedBy = actor;
        if (string.Equals(requirement.Status, RequirementStatusCreated, StringComparison.OrdinalIgnoreCase))
        {
            requirement.Status = RequirementStatusUpdated;
        }

        var existingItem = requirement.Items.FirstOrDefault();
        if (existingItem is null)
        {
            requirement.Items.Add(new MetalRequirementItem
            {
                Id = Guid.NewGuid(),
                MetalMaterialId = selectedMaterial.Id,
                ConsumptionPerUnit = selectedNorm.BaseConsumptionQty,
                ConsumptionUnit = selectedNorm.ConsumptionUnit,
                RequiredQty = requiredQty,
                RequiredWeightKg = calculation.NeedKg,
                SizeRaw = selectedNorm.SizeRaw,
                Comment = "Строка сформирована автоматически из нормы расхода.",
                NormPerUnit = selectedNorm.BaseConsumptionQty,
                TotalRequiredQty = requiredQty,
                Unit = selectedNorm.ConsumptionUnit,
                TotalRequiredWeightKg = calculation.NeedKg,
                CalculationFormula = calculation.Formula,
                CalculationInput = calculation.FormulaInput,
                SelectionSource = "auto_from_launch",
                SelectionReason = "Автоматическое обновление из запуска партии.",
                CandidateMaterials = null,
            });
            return;
        }

        existingItem.MetalMaterialId = selectedMaterial.Id;
        existingItem.ConsumptionPerUnit = selectedNorm.BaseConsumptionQty;
        existingItem.ConsumptionUnit = selectedNorm.ConsumptionUnit;
        existingItem.RequiredQty = requiredQty;
        existingItem.RequiredWeightKg = calculation.NeedKg;
        existingItem.SizeRaw = selectedNorm.SizeRaw;
        existingItem.Comment = "Строка обновлена автоматически из нормы расхода.";
        existingItem.NormPerUnit = selectedNorm.BaseConsumptionQty;
        existingItem.TotalRequiredQty = requiredQty;
        existingItem.Unit = selectedNorm.ConsumptionUnit;
        existingItem.TotalRequiredWeightKg = calculation.NeedKg;
        existingItem.CalculationFormula = calculation.Formula;
        existingItem.CalculationInput = calculation.FormulaInput;
        existingItem.SelectionSource = "auto_from_launch";
        existingItem.SelectionReason = "Автоматическое обновление из запуска партии.";

        if (requirement.Items.Count > 1)
        {
            _dbContext.MetalRequirementItems.RemoveRange(requirement.Items.Skip(1));
        }
    }

    private async Task<string> GetNextRequirementNumberAsync(CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"MR-{year}-";

        var lastNumber = await _dbContext.MetalRequirements
            .AsNoTracking()
            .Where(x => x.RequirementNumber.StartsWith(prefix))
            .OrderByDescending(x => x.RequirementNumber)
            .Select(x => x.RequirementNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(lastNumber))
        {
            return $"{prefix}000001";
        }

        var chunk = lastNumber[prefix.Length..];
        return int.TryParse(chunk, out var number)
            ? $"{prefix}{number + 1:D6}"
            : $"{prefix}000001";
    }
}
