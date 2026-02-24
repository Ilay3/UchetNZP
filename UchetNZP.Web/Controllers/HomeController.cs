using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Shared;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _dbContext;
        private static readonly string[] ServiceValues = ["-", "n/a", "null", "undefined", "служебное", "service"];
        private const int MinPoolSize = 20;
        private const int MaxPoolSize = 100;
        private const int MaxSourceTextLength = 120;
        private const int MaxDisplayTextLength = 42;

        public HomeController(ILogger<HomeController> logger, AppDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet("/api/background-bubbles/labels")]
        public async Task<IActionResult> GetBubbleLabels([FromQuery] int? in_limit, CancellationToken in_cancellationToken)
        {
            var requestedLimit = in_limit ?? 50;
            var limit = Math.Clamp(requestedLimit, MinPoolSize, MaxPoolSize);

            var rawLabels = await _dbContext.Parts
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => NameWithCodeFormatter.getNameWithCode(x.Name, x.Code))
                .Take(limit * 3)
                .ToListAsync(in_cancellationToken)
                .ConfigureAwait(false);

            var labels = rawLabels
                .Select(NormalizeLabel)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToList();

            return Ok(new BubbleLabelPoolResponse(labels));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private static string NormalizeLabel(string? in_raw)
        {
            if (string.IsNullOrWhiteSpace(in_raw))
            {
                return string.Empty;
            }

            var candidate = in_raw.Trim();

            if (candidate.Length > MaxSourceTextLength)
            {
                return string.Empty;
            }

            if (ServiceValues.Any(x => string.Equals(candidate, x, StringComparison.OrdinalIgnoreCase)))
            {
                return string.Empty;
            }

            if (candidate.Length <= MaxDisplayTextLength)
            {
                return candidate;
            }

            return string.Concat(candidate.AsSpan(0, MaxDisplayTextLength - 1), "…");
        }

        private sealed record BubbleLabelPoolResponse(IReadOnlyList<string> Items);
    }
}
