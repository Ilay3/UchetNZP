using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UchetNZP.Application.Contracts.Wip;

namespace UchetNZP.Application.Abstractions;

public interface IWipLabelService
{
    Task<IReadOnlyCollection<WipLabelDto>> GetLabelsAsync(WipLabelFilterDto? in_filter, CancellationToken in_cancellationToken = default);

    Task<WipLabelDto> CreateLabelAsync(WipLabelCreateDto in_request, CancellationToken in_cancellationToken = default);

    Task<IReadOnlyCollection<WipLabelDto>> CreateLabelsBatchAsync(WipLabelBatchCreateDto in_request, CancellationToken in_cancellationToken = default);

    Task<WipLabelDto> CreateLabelWithNumberAsync(WipLabelManualCreateDto in_request, CancellationToken in_cancellationToken = default);

    Task<WipLabelDto> UpdateLabelAsync(WipLabelUpdateDto in_request, CancellationToken in_cancellationToken = default);

    Task DeleteLabelAsync(Guid in_id, CancellationToken in_cancellationToken = default);
}
