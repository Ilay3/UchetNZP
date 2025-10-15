using System;
using System.Collections.Generic;
using System.Linq;

namespace UchetNZP.Web.Models;

public record PartOperationItemViewModel(
    int OpNumber,
    string OperationName,
    string SectionName,
    decimal NormHours);

public record PartWithOperationsViewModel(
    Guid PartId,
    string PartName,
    string? PartCode,
    IReadOnlyList<PartOperationItemViewModel> Operations)
{
    public string DisplayName => string.IsNullOrWhiteSpace(PartCode)
        ? PartName
        : $"{PartName} ({PartCode})";

    public decimal TotalNormHours => Operations.Sum(x => x.NormHours);
}
