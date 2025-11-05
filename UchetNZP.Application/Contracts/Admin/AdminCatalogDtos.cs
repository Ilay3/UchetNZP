using System;

namespace UchetNZP.Application.Contracts.Admin;

public record AdminPartDto(Guid Id, string Name, string? Code);

public record AdminPartEditDto(string Name, string? Code);

public record AdminOperationDto(Guid Id, string Name, string? Code);

public record AdminOperationEditDto(string Name, string? Code);

public record AdminSectionDto(Guid Id, string Name, string? Code);

public record AdminSectionEditDto(string Name, string? Code);

public record AdminWipBalanceDto(
    Guid Id,
    Guid PartId,
    string PartName,
    Guid SectionId,
    string SectionName,
    int OpNumber,
    decimal Quantity);

public record AdminWipBalanceEditDto(Guid PartId, Guid SectionId, int OpNumber, decimal Quantity);
