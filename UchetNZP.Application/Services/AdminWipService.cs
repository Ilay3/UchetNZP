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

    public async Task<string> ForceDeleteLabelAsync(Guid labelId, CancellationToken cancellationToken = default)
    {
        if (labelId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор ярлыка не задан.", nameof(labelId));
        }

        var label = await _dbContext.WipLabels
            .FirstOrDefaultAsync(x => x.Id == labelId, cancellationToken)
            .ConfigureAwait(false);

        if (label is null)
        {
            throw new KeyNotFoundException($"Ярлык с идентификатором {labelId} не найден.");
        }

        var labelNumber = label.Number;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var receipts = await _dbContext.WipReceipts
                .Where(x => x.WipLabelId == labelId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var receipt in receipts)
            {
                receipt.WipLabelId = null;
            }

            var transfers = await _dbContext.WipTransfers
                .Where(x => x.WipLabelId == labelId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var transfer in transfers)
            {
                transfer.WipLabelId = null;
            }

            var transferAudits = await _dbContext.TransferAudits
                .Where(x => x.WipLabelId == labelId || x.ResidualWipLabelId == labelId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var audit in transferAudits)
            {
                if (audit.WipLabelId == labelId)
                {
                    audit.WipLabelId = null;
                }

                if (audit.ResidualWipLabelId == labelId)
                {
                    audit.ResidualWipLabelId = null;
                }
            }

            var childLabels = await _dbContext.WipLabels
                .Where(x => x.Id != labelId && (x.ParentLabelId == labelId || x.RootLabelId == labelId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var childLabel in childLabels)
            {
                if (childLabel.ParentLabelId == labelId)
                {
                    childLabel.ParentLabelId = null;
                }

                if (childLabel.RootLabelId == labelId)
                {
                    childLabel.RootLabelId = childLabel.Id;
                    if (string.IsNullOrWhiteSpace(childLabel.RootNumber))
                    {
                        childLabel.RootNumber = childLabel.Number;
                    }
                }
            }

            var warehouseLabelItems = await _dbContext.WarehouseLabelItems
                .Where(x => x.WipLabelId == labelId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            _dbContext.WarehouseLabelItems.RemoveRange(warehouseLabelItems);

            var transferLabelUsages = await _dbContext.TransferLabelUsages
                .Where(x => x.FromLabelId == labelId || x.CreatedToLabelId == labelId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            _dbContext.TransferLabelUsages.RemoveRange(transferLabelUsages);

            var labelMerges = await _dbContext.LabelMerges
                .Where(x => x.InputLabelId == labelId || x.OutputLabelId == labelId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            _dbContext.LabelMerges.RemoveRange(labelMerges);

            var ledgerEntries = await _dbContext.WipLabelLedger
                .Where(x => x.FromLabelId == labelId || x.ToLabelId == labelId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            _dbContext.WipLabelLedger.RemoveRange(ledgerEntries);

            _dbContext.WipLabels.Remove(label);

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return labelNumber;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
