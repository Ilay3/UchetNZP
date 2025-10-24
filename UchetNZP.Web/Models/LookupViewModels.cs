using System;
using System.Collections.Generic;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Web.Models;

public record LookupItemViewModel(Guid Id, string Name, string? Code);

public record OperationLookupItemViewModel(
    Guid Id,
    string Name,
    string? Code,
    IReadOnlyList<string> Sections);

public record PartOperationViewModel(
    Guid PartId,
    string OpNumber,
    Guid OperationId,
    string OperationName,
    Guid SectionId,
    string SectionName,
    decimal NormHours);

public record ReceiptSummaryItemViewModel(
    Guid PartId,
    string OpNumber,
    Guid SectionId,
    decimal Quantity,
    decimal Was,
    decimal Become,
    Guid BalanceId,
    Guid ReceiptId);

public record ReceiptBatchSummaryViewModel(int Saved, IReadOnlyList<ReceiptSummaryItemViewModel> Items);

public record LaunchOperationLookupViewModel(
    string OpNumber,
    string OperationName,
    decimal NormHours,
    Guid SectionId,
    string SectionName,
    decimal Balance);

public record LaunchTailOperationViewModel(
    string OpNumber,
    string OperationName,
    decimal NormHours,
    Guid SectionId,
    string SectionName);

public record LaunchTailSummaryViewModel(
    IReadOnlyList<LaunchTailOperationViewModel> Operations,
    decimal SumNormHours);

public record LaunchBatchItemViewModel(
    Guid PartId,
    string FromOpNumber,
    Guid SectionId,
    decimal Quantity,
    decimal Remaining,
    decimal SumHoursToFinish,
    Guid LaunchId);

public record LaunchBatchSummaryViewModel(int Saved, IReadOnlyList<LaunchBatchItemViewModel> Items);

public record TransferOperationLookupViewModel(
    string OpNumber,
    string OperationName,
    decimal NormHours,
    decimal Balance,
    bool IsWarehouse);

public record TransferOperationBalanceViewModel(string OpNumber, Guid SectionId, decimal Balance);

public record TransferBalancesViewModel(
    TransferOperationBalanceViewModel From,
    TransferOperationBalanceViewModel To);

public record TransferScrapSummaryViewModel(
    Guid ScrapId,
    ScrapType ScrapType,
    decimal Quantity,
    string? Comment);

public record TransferSummaryItemViewModel(
    Guid PartId,
    string FromOpNumber,
    Guid FromSectionId,
    decimal FromBalanceBefore,
    decimal FromBalanceAfter,
    string ToOpNumber,
    Guid ToSectionId,
    decimal ToBalanceBefore,
    decimal ToBalanceAfter,
    decimal Quantity,
    Guid TransferId,
    TransferScrapSummaryViewModel? Scrap);

public record TransferBatchSummaryViewModel(int Saved, IReadOnlyList<TransferSummaryItemViewModel> Items);
