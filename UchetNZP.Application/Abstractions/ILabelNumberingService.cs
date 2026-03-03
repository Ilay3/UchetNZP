namespace UchetNZP.Application.Abstractions;

public interface ILabelNumberingService
{
    Task<int> GetNextSuffixAsync(string in_rootNumber, CancellationToken in_cancellationToken = default);
}
