using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Admin;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Application.Services;

public class AdminWipService : IAdminWipService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public AdminWipService(AppDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<AdminWipAdjustmentResultDto> AdjustBalanceAsync(AdminWipAdjustmentRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.BalanceId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор остатка не задан.", nameof(request));
        }

        if (request.NewQuantity < 0)
        {
            throw new InvalidOperationException("Количество не может быть отрицательным.");
        }

        var balance = await _dbContext.WipBalances
            .FirstOrDefaultAsync(x => x.Id == request.BalanceId, cancellationToken)
            .ConfigureAwait(false);

        if (balance is null)
        {
            throw new KeyNotFoundException($"Остаток с идентификатором {request.BalanceId} не найден.");
        }

        var trimmedComment = string.IsNullOrWhiteSpace(request.Comment)
            ? null
            : request.Comment.Trim();

        if (trimmedComment?.Length > 512)
        {
            throw new InvalidOperationException("Комментарий превышает 512 символов.");
        }

        var previous = balance.Quantity;
        if (previous == request.NewQuantity)
        {
            return new AdminWipAdjustmentResultDto(balance.Id, previous, previous, 0m, Guid.Empty);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            balance.Quantity = request.NewQuantity;

            var adjustment = new WipBalanceAdjustment
            {
                Id = Guid.NewGuid(),
                WipBalanceId = balance.Id,
                PartId = balance.PartId,
                SectionId = balance.SectionId,
                OpNumber = balance.OpNumber,
                PreviousQuantity = previous,
                NewQuantity = request.NewQuantity,
                Delta = request.NewQuantity - previous,
                Comment = trimmedComment,
                UserId = _currentUserService.UserId,
                CreatedAt = DateTime.UtcNow,
            };

            await _dbContext.WipBalanceAdjustments.AddAsync(adjustment, cancellationToken).ConfigureAwait(false);

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new AdminWipAdjustmentResultDto(balance.Id, previous, request.NewQuantity, adjustment.Delta, adjustment.Id);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<AdminWipBulkCleanupPreviewDto> PreviewBulkCleanupAsync(AdminWipBulkCleanupRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.MinQuantity < 0)
        {
            throw new InvalidOperationException("Минимальное количество не может быть отрицательным.");
        }

        var trimmedComment = string.IsNullOrWhiteSpace(request.Comment)
            ? null
            : request.Comment.Trim();

        if (trimmedComment?.Length > 512)
        {
            throw new InvalidOperationException("Комментарий превышает 512 символов.");
        }

        var query = _dbContext.WipBalances.AsNoTracking().AsQueryable();

        if (request.PartId.HasValue)
        {
            query = query.Where(x => x.PartId == request.PartId.Value);
        }

        if (request.SectionId.HasValue)
        {
            query = query.Where(x => x.SectionId == request.SectionId.Value);
        }

        if (request.OpNumber.HasValue)
        {
            query = query.Where(x => x.OpNumber == request.OpNumber.Value);
        }

        query = query.Where(x => x.Quantity >= request.MinQuantity && x.Quantity > 0m);

        var selectedBalances = await query
            .Select(x => new { x.Id, x.Quantity })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var jobId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var affectedCount = selectedBalances.Count;
        var affectedQuantity = selectedBalances.Sum(x => x.Quantity);

        var job = new WipBalanceCleanupJob
        {
            Id = jobId,
            UserId = _currentUserService.UserId,
            CreatedAt = now,
            PartId = request.PartId,
            SectionId = request.SectionId,
            OpNumber = request.OpNumber,
            MinQuantity = request.MinQuantity,
            AffectedCount = affectedCount,
            AffectedQuantity = affectedQuantity,
            Comment = trimmedComment,
            IsExecuted = false,
        };

        await _dbContext.WipBalanceCleanupJobs.AddAsync(job, cancellationToken).ConfigureAwait(false);

        if (affectedCount > 0)
        {
            var stageItems = selectedBalances.Select(x => new WipBalanceCleanupStageItem
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                WipBalanceId = x.Id,
                PreviousQuantity = x.Quantity,
            });

            await _dbContext.WipBalanceCleanupStageItems.AddRangeAsync(stageItems, cancellationToken).ConfigureAwait(false);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdminWipBulkCleanupPreviewDto(
            jobId,
            affectedCount,
            affectedQuantity,
            request.PartId,
            request.SectionId,
            request.OpNumber,
            request.MinQuantity);
    }

    public async Task<AdminWipBulkCleanupResultDto> ExecuteBulkCleanupAsync(AdminWipBulkCleanupExecuteDto request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (!request.Confirmed)
        {
            throw new InvalidOperationException("Операция не подтверждена.");
        }

        var job = await _dbContext.WipBalanceCleanupJobs
            .Include(x => x.StageItems)
            .FirstOrDefaultAsync(x => x.Id == request.JobId, cancellationToken)
            .ConfigureAwait(false);

        if (job is null)
        {
            throw new KeyNotFoundException($"Задание {request.JobId} не найдено.");
        }

        if (job.IsExecuted)
        {
            throw new InvalidOperationException("Это задание уже выполнено.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var stageBalanceIds = job.StageItems.Select(x => x.WipBalanceId).ToList();
            var balances = stageBalanceIds.Count == 0
                ? new List<WipBalance>()
                : await _dbContext.WipBalances
                    .Where(x => stageBalanceIds.Contains(x.Id) && x.Quantity > 0m)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

            var now = DateTime.UtcNow;
            var adjustments = new List<WipBalanceAdjustment>(balances.Count);

            foreach (var balance in balances)
            {
                var previousQuantity = balance.Quantity;
                balance.Quantity = 0m;

                adjustments.Add(new WipBalanceAdjustment
                {
                    Id = Guid.NewGuid(),
                    WipBalanceId = balance.Id,
                    PartId = balance.PartId,
                    SectionId = balance.SectionId,
                    OpNumber = balance.OpNumber,
                    PreviousQuantity = previousQuantity,
                    NewQuantity = 0m,
                    Delta = -previousQuantity,
                    Comment = $"BULK-CLEANUP:{job.Id:N}" + (string.IsNullOrWhiteSpace(job.Comment) ? string.Empty : $" {job.Comment}"),
                    UserId = _currentUserService.UserId,
                    CreatedAt = now,
                });
            }

            if (adjustments.Count > 0)
            {
                await _dbContext.WipBalanceAdjustments.AddRangeAsync(adjustments, cancellationToken).ConfigureAwait(false);
            }

            job.IsExecuted = true;
            job.ExecutedAt = now;
            job.AffectedCount = adjustments.Count;
            job.AffectedQuantity = adjustments.Sum(x => x.PreviousQuantity);

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new AdminWipBulkCleanupResultDto(job.Id, adjustments.Count, job.AffectedQuantity, now);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
