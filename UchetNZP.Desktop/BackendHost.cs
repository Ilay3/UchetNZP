using System.Diagnostics;
using System.Net.Http;

namespace UchetNZP.Desktop;

internal sealed class BackendHost : IAsyncDisposable
{
    private readonly Uri _baseUri;
    private Process? _process;

    public BackendHost(Uri baseUri)
    {
        _baseUri = baseUri;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_process is not null)
        {
            return;
        }

        var webProjectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "UchetNZP.Web", "UchetNZP.Web.csproj"));
        if (!File.Exists(webProjectPath))
        {
            throw new FileNotFoundException("Не найден проект UchetNZP.Web.", webProjectPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{webProjectPath}\" --urls {_baseUri}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Не удалось запустить backend-процесс.");
        _ = DrainStreamAsync(_process.StandardOutput, cancellationToken);
        _ = DrainStreamAsync(_process.StandardError, cancellationToken);

        await WaitForServerAsync(cancellationToken);
    }

    private async Task WaitForServerAsync(CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        for (var attempt = 0; attempt < 40; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_process?.HasExited == true)
            {
                throw new InvalidOperationException("Backend-процесс завершился до готовности приложения.");
            }

            try
            {
                using var response = await client.GetAsync(_baseUri, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // ignore until timeout is reached
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException("Backend не ответил вовремя.");
    }

    private static async Task DrainStreamAsync(StreamReader stream, CancellationToken cancellationToken)
    {
        while (!stream.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            await stream.ReadLineAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        catch
        {
            // ignored on shutdown
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }
}
