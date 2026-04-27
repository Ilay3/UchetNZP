namespace UchetNZP.Domain.Entities;

public class MetalAuditLog
{
    public Guid Id { get; set; }

    public DateTime EventDate { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string? DocumentNumber { get; set; }

    public string Message { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    public string? UserName { get; set; }

    public string? PayloadJson { get; set; }

    public DateTime CreatedAt { get; set; }
}
