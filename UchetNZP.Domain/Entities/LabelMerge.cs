namespace UchetNZP.Domain.Entities;

public class LabelMerge
{
    public Guid Id { get; set; }

    public Guid InputLabelId { get; set; }

    public Guid OutputLabelId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual WipLabel? InputLabel { get; set; }

    public virtual WipLabel? OutputLabel { get; set; }
}
