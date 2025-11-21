using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Admin;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Application.Services;

public class AdminCatalogService : IAdminCatalogService
{
    private readonly AppDbContext _dbContext;

    public AdminCatalogService(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyCollection<AdminPartDto>> GetPartsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Parts
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new AdminPartDto(x.Id, x.Name, x.Code))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AdminPartDto> CreatePartAsync(AdminPartEditDto input, CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var (name, code) = ValidateNameAndCode(input.Name, input.Code, "детали");

        var entity = new Part
        {
            Id = Guid.NewGuid(),
            Name = name,
            Code = code,
        };

        await _dbContext.Parts.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdminPartDto(entity.Id, entity.Name, entity.Code);
    }

    public async Task<AdminPartDto> UpdatePartAsync(Guid id, AdminPartEditDto input, CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var entity = await _dbContext.Parts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Деталь с идентификатором {id} не найдена.");

        var (name, code) = ValidateNameAndCode(input.Name, input.Code, "детали");

        entity.Name = name;
        entity.Code = code;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdminPartDto(entity.Id, entity.Name, entity.Code);
    }

    public async Task DeletePartAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Parts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Деталь с идентификатором {id} не найдена.");

        _dbContext.Parts.Remove(entity);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Не удалось удалить деталь из-за связанных записей.", ex);
        }
    }

    public async Task<IReadOnlyCollection<AdminOperationDto>> GetOperationsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Operations
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new AdminOperationDto(x.Id, x.Name, x.Code))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AdminOperationDto> CreateOperationAsync(AdminOperationEditDto input, CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var (name, code) = ValidateNameAndCode(input.Name, input.Code, "операции");

        var entity = new Operation
        {
            Id = Guid.NewGuid(),
            Name = name,
            Code = code,
        };

        await _dbContext.Operations.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdminOperationDto(entity.Id, entity.Name, entity.Code);
    }

    public async Task<AdminOperationDto> UpdateOperationAsync(Guid id, AdminOperationEditDto input, CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var entity = await _dbContext.Operations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Операция с идентификатором {id} не найдена.");

        var (name, code) = ValidateNameAndCode(input.Name, input.Code, "операции");

        entity.Name = name;
        entity.Code = code;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdminOperationDto(entity.Id, entity.Name, entity.Code);
    }

    public async Task DeleteOperationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Operations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Операция с идентификатором {id} не найдена.");

        _dbContext.Operations.Remove(entity);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Не удалось удалить операцию из-за связанных записей.", ex);
        }
    }

    public async Task<IReadOnlyCollection<AdminSectionDto>> GetSectionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sections
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new AdminSectionDto(x.Id, x.Name, x.Code))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AdminSectionDto> CreateSectionAsync(AdminSectionEditDto input, CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var (name, code) = ValidateNameAndCode(input.Name, input.Code, "участка");

        var entity = new Section
        {
            Id = Guid.NewGuid(),
            Name = name,
            Code = code,
        };

        await _dbContext.Sections.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdminSectionDto(entity.Id, entity.Name, entity.Code);
    }

    public async Task<AdminSectionDto> UpdateSectionAsync(Guid id, AdminSectionEditDto input, CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var entity = await _dbContext.Sections.FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Участок с идентификатором {id} не найден.");

        var (name, code) = ValidateNameAndCode(input.Name, input.Code, "участка");

        entity.Name = name;
        entity.Code = code;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdminSectionDto(entity.Id, entity.Name, entity.Code);
    }

    public async Task DeleteSectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Sections.FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Участок с идентификатором {id} не найден.");

        _dbContext.Sections.Remove(entity);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Не удалось удалить участок из-за связанных записей.", ex);
        }
    }

    public async Task<IReadOnlyCollection<AdminWipBalanceDto>> GetWipBalancesAsync(CancellationToken cancellationToken = default)
    {
        var routes = _dbContext.PartRoutes
            .AsNoTracking()
            .Include(x => x.Operation)
            .Select(x => new
            {
                x.PartId,
                x.SectionId,
                x.OpNumber,
                x.OperationId,
                OperationName = x.Operation != null ? x.Operation.Name : string.Empty,
                OperationLabel = x.Operation != null ? x.Operation.Code : null
            });

        return await (
                from balance in _dbContext.WipBalances
                    .AsNoTracking()
                    .Include(x => x.Part)
                    .Include(x => x.Section)
                join route in routes
                    on (balance.PartId, balance.SectionId, balance.OpNumber)
                    equals (route.PartId, route.SectionId, route.OpNumber) into routeGroup
                from route in routeGroup.DefaultIfEmpty()
                orderby balance.Part != null ? balance.Part.Name : string.Empty,
                    balance.Section != null ? balance.Section.Name : string.Empty,
                    balance.OpNumber
                select new AdminWipBalanceDto(
                    balance.Id,
                    balance.PartId,
                    balance.Part != null ? balance.Part.Name : string.Empty,
                    balance.SectionId,
                    balance.Section != null ? balance.Section.Name : string.Empty,
                    balance.OpNumber,
                    balance.Quantity,
                    route != null ? route.OperationId : null,
                    route != null ? route.OperationName : string.Empty,
                    route != null ? route.OperationLabel : null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AdminWipBalanceDto> CreateWipBalanceAsync(AdminWipBalanceEditDto input, CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        ValidateWipBalanceInput(input);

        await EnsurePartExistsAsync(input.PartId, cancellationToken).ConfigureAwait(false);
        await EnsureSectionExistsAsync(input.SectionId, cancellationToken).ConfigureAwait(false);

        var entity = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = input.PartId,
            SectionId = input.SectionId,
            OpNumber = input.OpNumber,
            Quantity = input.Quantity,
        };

        await _dbContext.WipBalances.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await MapWipBalanceAsync(entity.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdminWipBalanceDto> UpdateWipBalanceAsync(Guid id, AdminWipBalanceEditDto input, CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var entity = await _dbContext.WipBalances.FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Остаток с идентификатором {id} не найден.");

        ValidateWipBalanceInput(input);

        await EnsurePartExistsAsync(input.PartId, cancellationToken).ConfigureAwait(false);
        await EnsureSectionExistsAsync(input.SectionId, cancellationToken).ConfigureAwait(false);

        entity.PartId = input.PartId;
        entity.SectionId = input.SectionId;
        entity.OpNumber = input.OpNumber;
        entity.Quantity = input.Quantity;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await MapWipBalanceAsync(entity.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteWipBalanceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.WipBalances.FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Остаток с идентификатором {id} не найден.");

        _dbContext.WipBalances.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static (string Name, string? Code) ValidateNameAndCode(string? name, string? code, string entityDisplay)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException($"Наименование {entityDisplay} не может быть пустым.");
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > 256)
        {
            throw new InvalidOperationException($"Наименование {entityDisplay} превышает 256 символов.");
        }

        var trimmedCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        if (trimmedCode != null && trimmedCode.Length > 64)
        {
            throw new InvalidOperationException($"Код {entityDisplay} превышает 64 символа.");
        }

        return (trimmedName, trimmedCode);
    }

    private static void ValidateWipBalanceInput(AdminWipBalanceEditDto input)
    {
        if (input.OpNumber < 0)
        {
            throw new InvalidOperationException("Номер операции не может быть отрицательным.");
        }

        if (input.Quantity < 0)
        {
            throw new InvalidOperationException("Количество не может быть отрицательным.");
        }
    }

    private async Task EnsurePartExistsAsync(Guid partId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Parts.AnyAsync(x => x.Id == partId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            throw new KeyNotFoundException($"Деталь с идентификатором {partId} не найдена.");
        }
    }

    private async Task EnsureSectionExistsAsync(Guid sectionId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Sections.AnyAsync(x => x.Id == sectionId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            throw new KeyNotFoundException($"Участок с идентификатором {sectionId} не найден.");
        }
    }

    private async Task<AdminWipBalanceDto> MapWipBalanceAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.WipBalances
            .AsNoTracking()
            .Include(x => x.Part)
            .Include(x => x.Section)
            .Where(x => x.Id == id)
            .GroupJoin(
                _dbContext.PartRoutes
                    .AsNoTracking()
                    .Include(x => x.Operation),
                balance => new { balance.PartId, balance.SectionId, balance.OpNumber },
                route => new { route.PartId, route.SectionId, route.OpNumber },
                (balance, routeGroup) => new { balance, route = routeGroup.FirstOrDefault() })
            .Select(x => new AdminWipBalanceDto(
                x.balance.Id,
                x.balance.PartId,
                x.balance.Part != null ? x.balance.Part.Name : string.Empty,
                x.balance.SectionId,
                x.balance.Section != null ? x.balance.Section.Name : string.Empty,
                x.balance.OpNumber,
                x.balance.Quantity,
                x.route != null ? x.route.OperationId : null,
                x.route != null && x.route.Operation != null ? x.route.Operation.Name : string.Empty,
                x.route != null && x.route.Operation != null ? x.route.Operation.Code : null))
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
