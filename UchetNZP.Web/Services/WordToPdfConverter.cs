using System.Diagnostics;

namespace UchetNZP.Web.Services;

public interface IWordToPdfConverter
{
    Task<WordToPdfResult> ConvertAsync(
        string sourceFileName,
        byte[] docxContent,
        string tempFolderName,
        CancellationToken cancellationToken = default);
}

public sealed record WordToPdfResult(string FileName, string ContentType, byte[] Content);

public sealed class LibreOfficeWordToPdfConverter : IWordToPdfConverter
{
    private readonly IConfiguration _configuration;

    public LibreOfficeWordToPdfConverter(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<WordToPdfResult> ConvertAsync(
        string sourceFileName,
        byte[] docxContent,
        string tempFolderName,
        CancellationToken cancellationToken = default)
    {
        if (docxContent is null || docxContent.Length == 0)
        {
            throw new InvalidOperationException("DOCX document is empty.");
        }

        var sofficePath = _configuration["LibreOffice:ExecutablePath"];
        if (string.IsNullOrWhiteSpace(sofficePath) || !File.Exists(sofficePath))
        {
            throw new FileNotFoundException("LibreOffice executable was not found. Check LibreOffice:ExecutablePath.", sofficePath);
        }

        var safeFolderName = SanitizePathPart(tempFolderName, "documents");
        var tempRoot = Path.Combine(Path.GetTempPath(), "uchetnzp-documents", safeFolderName, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var docxFileName = EnsureDocxExtension(SanitizePathPart(sourceFileName, "document.docx"));
            var docxPath = Path.Combine(tempRoot, docxFileName);
            await File.WriteAllBytesAsync(docxPath, docxContent, cancellationToken).ConfigureAwait(false);

            var startInfo = new ProcessStartInfo
            {
                FileName = sofficePath,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("--headless");
            startInfo.ArgumentList.Add("--convert-to");
            startInfo.ArgumentList.Add("pdf");
            startInfo.ArgumentList.Add("--outdir");
            startInfo.ArgumentList.Add(tempRoot);
            startInfo.ArgumentList.Add(docxPath);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("LibreOffice process failed to start.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var details = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                throw new InvalidOperationException($"LibreOffice conversion failed: {details}");
            }

            var pdfPath = Path.ChangeExtension(docxPath, ".pdf");
            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException("LibreOffice did not create a PDF file.", pdfPath);
            }

            var pdfBytes = await File.ReadAllBytesAsync(pdfPath, cancellationToken).ConfigureAwait(false);
            return new WordToPdfResult(Path.GetFileName(pdfPath), "application/pdf", pdfBytes);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    private static string EnsureDocxExtension(string value)
    {
        return value.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
            ? value
            : string.Concat(value, ".docx");
    }

    private static string SanitizePathPart(string? value, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(normalized.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }
}
