namespace UchetNZP.Application.Contracts.Admin;

public record AdminWipAdjustmentRequestDto(Guid BalanceId, decimal NewQuantity, string? Comment);
