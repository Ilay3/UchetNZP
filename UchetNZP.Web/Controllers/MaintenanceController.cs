using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Web.Controllers;

[Route("internal/maintenance")]
[ApiExplorerSettings(IgnoreApi = true)]
public class MaintenanceController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(AppDbContext dbContext, IWebHostEnvironment environment, ILogger<MaintenanceController> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("clear-database")]
    public async Task<IActionResult> ClearDatabase(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
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
