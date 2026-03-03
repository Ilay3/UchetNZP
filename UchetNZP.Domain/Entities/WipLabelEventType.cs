namespace UchetNZP.Domain.Entities;

public enum WipLabelEventType
{
    Receipt = 1,
    Move = 2,
    Split = 3,
    Transfer = 4,
    Scrap = 5,
    Merge = 6,
    Revert = 7,
}
