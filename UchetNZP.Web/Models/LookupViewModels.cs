using System;
using System.Collections.Generic;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Web.Models;

public record LookupItemViewModel(Guid Id, string Name, string? Code);

public record PartOperationViewModel(
    Guid PartId,
    int OpNumber,
    Guid OperationId,
    string OperationName,
    Guid SectionId,
    string SectionName,
    decimal NormHours);

public record ReceiptSummaryItemViewModel(
    Guid PartId,
    int OpNumber,
    Guid SectionId,
    decimal Quantity,
    decimal Was,
    decimal Become,
    Guid BalanceId,
    Guid ReceiptId);

public record ReceiptBatchSummaryViewModel(int Saved, IReadOnlyList<ReceiptSummaryItemViewModel> Items);

public record LaunchOperationLookupViewModel(
    int OpNumber,
    string OperationName,
    decimal NormHours,
    Guid SectionId,
    string SectionName,
    decimal Balance);

public record LaunchTailOperationViewModel(
    int OpNumber,
    string OperationName,
    decimal NormHours,
    Guid SectionId,
    string SectionName);

public record LaunchTailSummaryViewModel(
    IReadOnlyList<LaunchTailOperationViewModel> Operations,
    decimal SumNormHours);

public record LaunchBatchItemViewModel(
    Guid PartId,
    int FromOpNumber,
    Guid SectionId,
    decimal Quantity,
    decimal Remaining,
    decimal SumHoursToFinish,
    Guid LaunchId);

public record LaunchBatchSummaryViewModel(int Saved, IReadOnlyList<LaunchBatchItemViewModel> Items);

public record TransferOperationLookupViewModel(
    int OpNumber,
    string OperationName,
    decimal NormHours,
    decimal Balance);

public record TransferOperationBalanceViewModel(int OpNumber, Guid SectionId, decimal Balance);

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
    int FromOpNumber,
    Guid FromSectionId,
    decimal FromBalanceBefore,
    decimal FromBalanceAfter,
    int ToOpNumber,
    Guid ToSectionId,
    decimal ToBalanceBefore,
    decimal ToBalanceAfter,
    decimal Quantity,
    Guid TransferId,
    TransferScrapSummaryViewModel? Scrap);

public record TransferBatchSummaryViewModel(int Saved, IReadOnlyList<TransferSummaryItemViewModel> Items);
