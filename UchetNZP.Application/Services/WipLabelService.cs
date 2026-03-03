using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Wip;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Application.Services;

public class WipLabelService : IWipLabelService
{
    private readonly AppDbContext m_dbContext;

    public WipLabelService(AppDbContext in_dbContext)
    {
        m_dbContext = in_dbContext ?? throw new ArgumentNullException(nameof(in_dbContext));
    }

    public async Task<IReadOnlyCollection<WipLabelDto>> GetLabelsAsync(WipLabelFilterDto? in_filter, CancellationToken in_cancellationToken = default)
    {
        var query = m_dbContext.WipLabels
            .AsNoTracking()
            .Include(x => x.Part)
            .AsQueryable();

        if (in_filter != null)
        {
            if (in_filter.PartId.HasValue && in_filter.PartId.Value != Guid.Empty)
            {
                var partId = in_filter.PartId.Value;
                query = query.Where(x => x.PartId == partId);
            }

            if (in_filter.From.HasValue)
            {
                var fromDate = NormalizeDate(in_filter.From.Value);
                query = query.Where(x => x.LabelDate >= fromDate);
            }

            if (in_filter.To.HasValue)
            {
                var toDate = NormalizeDate(in_filter.To.Value).AddDays(1).AddTicks(-1);
                query = query.Where(x => x.LabelDate <= toDate);
            }
        }

        var entities = await query
            .OrderByDescending(x => x.LabelDate)
            .ThenBy(x => x.Number)
            .ToListAsync(in_cancellationToken)
            .ConfigureAwait(false);

        var ret = entities
            .Select(x => new WipLabelDto(
                x.Id,
                x.PartId,
                x.Part != null ? x.Part.Name : string.Empty,
                x.Part != null ? x.Part.Code : null,
                x.Number,
                x.LabelDate,
                x.Quantity,
                x.IsAssigned))
            .ToList();

        return ret;
    }

    public async Task<WipLabelDto> CreateLabelAsync(WipLabelCreateDto in_request, CancellationToken in_cancellationToken = default)
    {
        if (in_request is null)
        {
            throw new ArgumentNullException(nameof(in_request));
        }

        var batchResult = await CreateLabelsAsync(
            in_request.PartId,
            in_request.LabelDate,
            in_request.Quantity,
            1,
            in_cancellationToken)
            .ConfigureAwait(false);

        var ret = batchResult[0];
        return ret;
    }

    public async Task<IReadOnlyCollection<WipLabelDto>> CreateLabelsBatchAsync(WipLabelBatchCreateDto in_request, CancellationToken in_cancellationToken = default)
    {
        if (in_request is null)
        {
            throw new ArgumentNullException(nameof(in_request));
        }

        if (in_request.Count <= 0)
        {
            throw new InvalidOperationException("Количество создаваемых ярлыков должно быть больше нуля.");
        }

        var ret = await CreateLabelsAsync(
            in_request.PartId,
            in_request.LabelDate,
            in_request.Quantity,
            in_request.Count,
            in_cancellationToken)
            .ConfigureAwait(false);

        return ret;
    }

    public async Task<WipLabelDto> CreateLabelWithNumberAsync(WipLabelManualCreateDto in_request, CancellationToken in_cancellationToken = default)
    {
        if (in_request is null)
        {
            throw new ArgumentNullException(nameof(in_request));
        }

        var normalizedNumber = NormalizeNumber(in_request.Number);
        var normalizedDate = NormalizeDate(in_request.LabelDate);

        if (in_request.PartId == Guid.Empty)
        {
            throw new InvalidOperationException("Не выбрана деталь для создания ярлыка.");
        }

        WipLabelInvariants.EnsurePositiveLabelQuantity(in_request.Quantity);

        await using var transaction = await m_dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, in_cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var exists = await m_dbContext.WipLabels
                .AsNoTracking()
                .AnyAsync(x => x.Number == normalizedNumber, in_cancellationToken)
                .ConfigureAwait(false);

            if (exists)
            {
                throw new InvalidOperationException($"Ярлык с номером {normalizedNumber} уже существует.");
            }

            var part = await GetPartAsync(in_request.PartId, in_cancellationToken).ConfigureAwait(false);

            var entity = new WipLabel
            {
                Id = Guid.NewGuid(),
                PartId = in_request.PartId,
                LabelDate = normalizedDate,
                Quantity = in_request.Quantity,
                RemainingQuantity = in_request.Quantity,
                Number = normalizedNumber,
                IsAssigned = false,
                Status = WipLabelStatus.Active,
                RootLabelId = Guid.Empty,
                ParentLabelId = null,
                RootNumber = string.Empty,
                Suffix = 0,
            };

            InitializeIdentity(entity, normalizedNumber);

            await m_dbContext.WipLabels.AddAsync(entity, in_cancellationToken).ConfigureAwait(false);
            await m_dbContext.SaveChangesAsync(in_cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(in_cancellationToken).ConfigureAwait(false);

            var ret = new WipLabelDto(
                entity.Id,
                entity.PartId,
                part.Name,
                part.Code,
                entity.Number,
                entity.LabelDate,
                entity.Quantity,
                entity.IsAssigned);

            return ret;
        }
        catch
        {
            await transaction.RollbackAsync(in_cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<WipLabelDto> UpdateLabelAsync(WipLabelUpdateDto in_request, CancellationToken in_cancellationToken = default)
    {
        if (in_request is null)
        {
            throw new ArgumentNullException(nameof(in_request));
        }

        if (in_request.Id == Guid.Empty)
        {
            throw new InvalidOperationException("Не указан идентификатор ярлыка.");
        }

        WipLabelInvariants.EnsurePositiveLabelQuantity(in_request.Quantity);

        var normalizedNumber = NormalizeNumber(in_request.Number);
        var normalizedDate = NormalizeDate(in_request.LabelDate);

        var label = await m_dbContext.WipLabels
            .Include(x => x.Part)
            .FirstOrDefaultAsync(x => x.Id == in_request.Id, in_cancellationToken)
            .ConfigureAwait(false);

        if (label == null)
        {
            throw new InvalidOperationException("Ярлык не найден.");
        }

        await EnsureLabelCanBeModifiedAsync(label.Id, in_cancellationToken).ConfigureAwait(false);

        if (!string.Equals(label.Number, normalizedNumber, StringComparison.Ordinal))
        {
            var exists = await m_dbContext.WipLabels
                .AsNoTracking()
                .AnyAsync(x => x.Number == normalizedNumber && x.Id != label.Id, in_cancellationToken)
                .ConfigureAwait(false);

            if (exists)
            {
                throw new InvalidOperationException($"Ярлык с номером {normalizedNumber} уже существует.");
            }
        }

        label.Number = normalizedNumber;
        label.LabelDate = normalizedDate;
        var parsedNumber = WipLabelInvariants.ParseNumber(normalizedNumber);
        label.RootNumber = parsedNumber.RootNumber;
        label.Suffix = parsedNumber.Suffix;
        label.Quantity = in_request.Quantity;
        label.RemainingQuantity = in_request.Quantity;

        await m_dbContext.SaveChangesAsync(in_cancellationToken).ConfigureAwait(false);

        var partName = label.Part != null ? label.Part.Name : string.Empty;
        var partCode = label.Part != null ? label.Part.Code : null;

        var ret = new WipLabelDto(
            label.Id,
            label.PartId,
            partName,
            partCode,
            label.Number,
            label.LabelDate,
            label.Quantity,
            label.IsAssigned);

        return ret;
    }

    public async Task<WipLabelStateDto> GetLabelStateAsync(Guid in_id, CancellationToken in_cancellationToken = default)
    {
        if (in_id == Guid.Empty)
        {
            throw new InvalidOperationException("Не указан идентификатор ярлыка.");
        }

        var state = await m_dbContext.WipLabels
            .AsNoTracking()
            .Where(x => x.Id == in_id)
            .Select(x => new WipLabelStateDto(
                x.Id,
                x.Number,
                x.Status.ToString(),
                x.CurrentSectionId,
                x.CurrentOpNumber,
                x.RootLabelId,
                x.ParentLabelId,
                x.RootNumber,
                x.Suffix,
                x.Quantity,
                x.RemainingQuantity))
            .FirstOrDefaultAsync(in_cancellationToken)
            .ConfigureAwait(false);

        if (state is null)
        {
            throw new InvalidOperationException("Ярлык не найден.");
        }

        return state;
    }

    public async Task<IReadOnlyCollection<WipLabelLedgerEventDto>> GetLabelLedgerAsync(Guid in_id, CancellationToken in_cancellationToken = default)
    {
        if (in_id == Guid.Empty)
        {
            throw new InvalidOperationException("Не указан идентификатор ярлыка.");
        }

        var exists = await m_dbContext.WipLabels
            .AsNoTracking()
            .AnyAsync(x => x.Id == in_id, in_cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            throw new InvalidOperationException("Ярлык не найден.");
        }

        var events = await m_dbContext.WipLabelLedger
            .AsNoTracking()
            .Where(x => x.FromLabelId == in_id || x.ToLabelId == in_id)
            .OrderBy(x => x.EventTime)
            .ThenBy(x => x.EventId)
            .Select(x => new WipLabelLedgerEventDto(
                x.EventId,
                x.EventTime,
                x.UserId,
                x.TransactionId,
                x.EventType.ToString(),
                x.FromLabelId,
                x.ToLabelId,
                x.FromSectionId,
                x.FromOpNumber,
                x.ToSectionId,
                x.ToOpNumber,
                x.Qty,
                x.ScrapQty,
                x.RefEntityType,
                x.RefEntityId))
            .ToListAsync(in_cancellationToken)
            .ConfigureAwait(false);

        return events;
    }

    public async Task DeleteLabelAsync(Guid in_id, CancellationToken in_cancellationToken = default)
    {
        if (in_id == Guid.Empty)
        {
            throw new InvalidOperationException("Не указан идентификатор ярлыка.");
        }

        var label = await m_dbContext.WipLabels
            .FirstOrDefaultAsync(x => x.Id == in_id, in_cancellationToken)
            .ConfigureAwait(false);

        if (label == null)
        {
            throw new InvalidOperationException("Ярлык не найден.");
        }

        await EnsureLabelCanBeModifiedAsync(label.Id, in_cancellationToken).ConfigureAwait(false);

        m_dbContext.WipLabels.Remove(label);
        await m_dbContext.SaveChangesAsync(in_cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<WipLabelDto>> CreateLabelsAsync(Guid in_partId, DateTime in_labelDate, decimal in_quantity, int in_count, CancellationToken in_cancellationToken)
    {
        if (in_partId == Guid.Empty)
        {
            throw new InvalidOperationException("Не выбрана деталь для создания ярлыка.");
        }

        WipLabelInvariants.EnsurePositiveLabelQuantity(in_quantity);

        if (in_count <= 0)
        {
            throw new InvalidOperationException("Количество ярлыков должно быть больше нуля.");
        }

        await using var transaction = await m_dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, in_cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var part = await GetPartAsync(in_partId, in_cancellationToken).ConfigureAwait(false);
            var normalizedDate = NormalizeDate(in_labelDate);
            var numbers = await GenerateSequentialNumbersAsync(in_count, in_cancellationToken).ConfigureAwait(false);
            var entities = new List<WipLabel>(numbers.Count);

            foreach (var number in numbers)
            {
                var label = new WipLabel
                {
                    Id = Guid.NewGuid(),
                    PartId = in_partId,
                    LabelDate = normalizedDate,
                    Quantity = in_quantity,
                    RemainingQuantity = in_quantity,
                    Number = number,
                    IsAssigned = false,
                Status = WipLabelStatus.Active,
                RootLabelId = Guid.Empty,
                ParentLabelId = null,
                RootNumber = string.Empty,
                Suffix = 0,
                };

                InitializeIdentity(label, number);
                entities.Add(label);
            }

            await m_dbContext.WipLabels.AddRangeAsync(entities, in_cancellationToken).ConfigureAwait(false);
            await m_dbContext.SaveChangesAsync(in_cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(in_cancellationToken).ConfigureAwait(false);

            var ret = entities
                .Select(x => new WipLabelDto(x.Id, x.PartId, part.Name, part.Code, x.Number, x.LabelDate, x.Quantity, x.IsAssigned))
                .ToList();

            return ret;
        }
        catch
        {
            await transaction.RollbackAsync(in_cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static void InitializeIdentity(WipLabel label, string number)
    {
        var parsedNumber = WipLabelInvariants.ParseNumber(number);
        label.RootLabelId = label.Id;
        label.ParentLabelId = null;
        label.RootNumber = parsedNumber.RootNumber;
        label.Suffix = parsedNumber.Suffix;
        label.Status = WipLabelStatus.Active;
        label.CurrentSectionId = null;
        label.CurrentOpNumber = null;
    }

    private async Task<Part> GetPartAsync(Guid in_partId, CancellationToken in_cancellationToken)
    {
        var part = await m_dbContext.Parts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == in_partId, in_cancellationToken)
            .ConfigureAwait(false);

        if (part == null)
        {
            throw new InvalidOperationException($"Деталь с идентификатором {in_partId} не найдена.");
        }

        var ret = part;
        return ret;
    }

    private async Task<List<string>> GenerateSequentialNumbersAsync(int in_count, CancellationToken in_cancellationToken)
    {
        var existingRoots = await m_dbContext.WipLabels
            .AsNoTracking()
            .Where(x => x.Suffix == 0 && !string.IsNullOrWhiteSpace(x.RootNumber))
            .Select(x => x.RootNumber)
            .ToListAsync(in_cancellationToken)
            .ConfigureAwait(false);

        var start = 0;
        foreach (var rootNumber in existingRoots)
        {
            if (int.TryParse(rootNumber, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedRoot) && parsedRoot > start)
            {
                start = parsedRoot;
            }
        }

        var ret = new List<string>(in_count);

        for (var index = 1; index <= in_count; index++)
        {
            var next = start + index;
            var rootNumber = next.ToString("D5", CultureInfo.InvariantCulture);
            ret.Add(WipLabelInvariants.FormatNumber(rootNumber, 0));
        }

        return ret;
    }

    private static DateTime NormalizeDate(DateTime in_date)
    {
        var ret = DateTime.SpecifyKind(in_date.Date, DateTimeKind.Utc);
        return ret;
    }

    private static string NormalizeNumber(string in_number)
    {
        if (string.IsNullOrWhiteSpace(in_number))
        {
            throw new InvalidOperationException("Номер ярлыка не может быть пустым.");
        }

        var parsed = WipLabelInvariants.ParseNumber(in_number);

        if (parsed.RootNumber.Length is < 1 or > 32 || !parsed.RootNumber.All(char.IsDigit))
        {
            throw new InvalidOperationException("Номер ярлыка должен быть в формате 12345 или 12345/1.");
        }

        if (!int.TryParse(parsed.RootNumber, NumberStyles.None, CultureInfo.InvariantCulture, out var baseNumber) || baseNumber <= 0)
        {
            throw new InvalidOperationException("Номер ярлыка должен быть положительным числом.");
        }

        var normalizedBase = baseNumber.ToString("D5", CultureInfo.InvariantCulture);
        if (parsed.Suffix == 0)
        {
            return WipLabelInvariants.FormatNumber(normalizedBase, 0);
        }

        if (parsed.Suffix < 0)
        {
            throw new InvalidOperationException("Суффикс номера ярлыка должен быть неотрицательным целым числом.");
        }

        return WipLabelInvariants.FormatNumber(normalizedBase, parsed.Suffix);
    }

    private async Task EnsureLabelCanBeModifiedAsync(Guid in_labelId, CancellationToken in_cancellationToken)
    {
        var label = await m_dbContext.WipLabels
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == in_labelId, in_cancellationToken)
            .ConfigureAwait(false);

        if (label == null)
        {
            throw new InvalidOperationException("Ярлык не найден.");
        }

        if (label.RemainingQuantity != label.Quantity)
        {
            throw new InvalidOperationException("Нельзя изменить или удалить ярлык, по которому уже списан остаток.");
        }

        var hasReceipt = await m_dbContext.WipReceipts
            .AsNoTracking()
            .AnyAsync(x => x.WipLabelId == in_labelId, in_cancellationToken)
            .ConfigureAwait(false);

        if (hasReceipt)
        {
            throw new InvalidOperationException("Нельзя изменить или удалить ярлык, связанный с приёмкой.");
        }

        var hasTransfers = await m_dbContext.WipTransfers
            .AsNoTracking()
            .AnyAsync(x => x.WipLabelId == in_labelId, in_cancellationToken)
            .ConfigureAwait(false);

        if (hasTransfers)
        {
            throw new InvalidOperationException("Нельзя изменить или удалить ярлык, использованный в перемещениях.");
        }

        var hasWarehouseItems = await m_dbContext.WarehouseLabelItems
            .AsNoTracking()
            .AnyAsync(x => x.WipLabelId == in_labelId, in_cancellationToken)
            .ConfigureAwait(false);

        if (hasWarehouseItems)
        {
            throw new InvalidOperationException("Нельзя изменить или удалить ярлык, находящийся на складе.");
        }
    }
}
