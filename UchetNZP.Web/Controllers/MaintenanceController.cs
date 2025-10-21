using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Configuration;

namespace UchetNZP.Web.Controllers;

[Route("internal/maintenance")]
[ApiExplorerSettings(IgnoreApi = true)]
public class MaintenanceController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<MaintenanceController> _logger;
    private readonly MaintenanceOptions _options;

    public MaintenanceController(
        AppDbContext dbContext,
        ILogger<MaintenanceController> logger,
        IOptions<MaintenanceOptions> options)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    [HttpPost("clear-database")]
    public async Task<IActionResult> ClearDatabase(CancellationToken cancellationToken)
    {
        if (!_options.AllowClearDatabaseEndpoint)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                message = "Команда очистки базы данных отключена настройками.",
            });
        }

        try
        {
            await _dbContext.Database.EnsureDeletedAsync(cancellationToken).ConfigureAwait(false);
            await _dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Database was cleared via hidden maintenance command.");
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear the database via hidden maintenance command.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = ex.Message,
            });
        }
    }
}
