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
            .Take(250)
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

    private async Task<List<WipLabelDto>> CreateLabelsAsync(Guid in_partId, DateTime in_labelDate, decimal in_quantity, int in_count, CancellationToken in_cancellationToken)
    {
        if (in_partId == Guid.Empty)
        {
            throw new InvalidOperationException("Не выбрана деталь для создания ярлыка.");
        }

        if (in_quantity <= 0)
        {
            throw new InvalidOperationException("Количество должно быть больше нуля.");
        }

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
                    Number = number,
                    IsAssigned = false,
                };

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
        var lastNumber = await m_dbContext.WipLabels
            .AsNoTracking()
            .OrderByDescending(x => x.Number)
            .Select(x => x.Number)
            .FirstOrDefaultAsync(in_cancellationToken)
            .ConfigureAwait(false);

        var start = 0;

        if (!string.IsNullOrWhiteSpace(lastNumber))
        {
            if (!int.TryParse(lastNumber, NumberStyles.None, CultureInfo.InvariantCulture, out start))
            {
                throw new InvalidOperationException($"Не удалось разобрать номер ярлыка {lastNumber}.");
            }
        }

        var ret = new List<string>(in_count);

        for (var index = 1; index <= in_count; index++)
        {
            var next = start + index;
            ret.Add(next.ToString("D5", CultureInfo.InvariantCulture));
        }

        return ret;
    }

    private static DateTime NormalizeDate(DateTime in_date)
    {
        var ret = DateTime.SpecifyKind(in_date.Date, DateTimeKind.Unspecified);
        return ret;
    }
}
