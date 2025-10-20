namespace UchetNZP.Application.Contracts.Admin;

public record AdminWipAdjustmentResultDto(
    Guid BalanceId,
    decimal PreviousQuantity,
    decimal NewQuantity,
    decimal Delta,
    Guid AdjustmentId);
