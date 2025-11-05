using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UchetNZP.Application.Contracts.Admin;

namespace UchetNZP.Application.Abstractions;

public interface IAdminCatalogService
{
    Task<IReadOnlyCollection<AdminPartDto>> GetPartsAsync(CancellationToken cancellationToken = default);

    Task<AdminPartDto> CreatePartAsync(AdminPartEditDto input, CancellationToken cancellationToken = default);

    Task<AdminPartDto> UpdatePartAsync(Guid id, AdminPartEditDto input, CancellationToken cancellationToken = default);

    Task DeletePartAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AdminOperationDto>> GetOperationsAsync(CancellationToken cancellationToken = default);

    Task<AdminOperationDto> CreateOperationAsync(AdminOperationEditDto input, CancellationToken cancellationToken = default);

    Task<AdminOperationDto> UpdateOperationAsync(Guid id, AdminOperationEditDto input, CancellationToken cancellationToken = default);

    Task DeleteOperationAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AdminSectionDto>> GetSectionsAsync(CancellationToken cancellationToken = default);

    Task<AdminSectionDto> CreateSectionAsync(AdminSectionEditDto input, CancellationToken cancellationToken = default);

    Task<AdminSectionDto> UpdateSectionAsync(Guid id, AdminSectionEditDto input, CancellationToken cancellationToken = default);

    Task DeleteSectionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AdminWipBalanceDto>> GetWipBalancesAsync(CancellationToken cancellationToken = default);

    Task<AdminWipBalanceDto> CreateWipBalanceAsync(AdminWipBalanceEditDto input, CancellationToken cancellationToken = default);

    Task<AdminWipBalanceDto> UpdateWipBalanceAsync(Guid id, AdminWipBalanceEditDto input, CancellationToken cancellationToken = default);

    Task DeleteWipBalanceAsync(Guid id, CancellationToken cancellationToken = default);
}
