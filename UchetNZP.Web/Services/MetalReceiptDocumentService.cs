using Microsoft.EntityFrameworkCore;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Web.Services;

public interface IMetalReceiptDocumentService
{
    Task<MetalReceiptDocumentResult> BuildAsync(Guid receiptId, CancellationToken cancellationToken = default);
}

public sealed record MetalReceiptDocumentResult(string FileName, string ContentType, byte[] Content);

public class MetalReceiptDocumentService : IMetalReceiptDocumentService
{
    private const string TemplateRelativePath = "Templates/Documents/Приходный ордер.docx";

    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public MetalReceiptDocumentService(AppDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    public async Task<MetalReceiptDocumentResult> BuildAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var receipt = await _dbContext.MetalReceipts
            .AsNoTracking()
            .Where(x => x.Id == receiptId)
            .Select(x => new { x.ReceiptNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (receipt is null)
            throw new KeyNotFoundException();

        var templatePath = Path.Combine(_environment.ContentRootPath, TemplateRelativePath);
        if (!File.Exists(templatePath))
            throw new FileNotFoundException("Шаблон печатной формы приходного ордера не найден в проекте.", templatePath);

        var content = await File.ReadAllBytesAsync(templatePath, cancellationToken);
        var fileName = $"Приходный_ордер_{receipt.ReceiptNumber}.docx";
        const string contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        return new MetalReceiptDocumentResult(fileName, contentType, content);
    }
}
