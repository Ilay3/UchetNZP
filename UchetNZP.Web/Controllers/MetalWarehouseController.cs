using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Controllers;

[Route("MetalWarehouse")]
public class MetalWarehouseController : Controller
{
    private readonly AppDbContext _dbContext;

    public MetalWarehouseController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new MetalWarehouseDashboardViewModel
        {
            MaterialsInCatalog = await _dbContext.Parts.AsNoTracking().CountAsync(cancellationToken),
            MetalUnitsInStock = await _dbContext.WarehouseItems.AsNoTracking().SumAsync(x => (decimal?)x.Quantity, cancellationToken) ?? 0m,
            OpenRequirements = 0,
            MovementsToday = 0,
        };

        return View(model);
    }

    [HttpGet("Receipts")]
    public IActionResult Receipts()
    {
        var model = new MetalWarehouseListPageViewModel
        {
            Title = "Приход металла",
            Description = "Журнал поступлений металла в складской модуль.",
            Headers = new[] { "Дата", "Материал", "Партия", "Количество", "Комментарий" },
            EmptyStateTitle = "Поступлений пока нет",
            EmptyStateDescription = "Когда вы добавите первый приход металла, он появится в этой таблице.",
        };

        return View(model);
    }

    [HttpGet("Stock")]
    public IActionResult Stock()
    {
        var model = new MetalWarehouseListPageViewModel
        {
            Title = "Остатки металла",
            Description = "Текущая сводка по количеству металла на складе.",
            Headers = new[] { "Материал", "Марка", "Ед. изм.", "Количество", "Обновлено" },
            EmptyStateTitle = "Остатков пока нет",
            EmptyStateDescription = "Остатки будут отображаться после загрузки или прихода металла.",
        };

        return View(model);
    }

    [HttpGet("Requirements")]
    public IActionResult Requirements()
    {
        var model = new MetalWarehouseListPageViewModel
        {
            Title = "Требования производства",
            Description = "Запросы цеха на выдачу металла.",
            Headers = new[] { "№ требования", "Дата", "Цех", "Материал", "Требуемо", "Статус" },
            EmptyStateTitle = "Открытых требований нет",
            EmptyStateDescription = "Новые требования появятся здесь после подключения процесса выдачи.",
        };

        return View(model);
    }

    [HttpGet("Movements")]
    public IActionResult Movements()
    {
        var model = new MetalWarehouseListPageViewModel
        {
            Title = "История движений",
            Description = "Хронология всех операций по складу металла.",
            Headers = new[] { "Дата и время", "Операция", "Материал", "Количество", "Источник", "Ответственный" },
            EmptyStateTitle = "Движений пока нет",
            EmptyStateDescription = "История начнёт наполняться после проведения операций в модуле.",
        };

        return View(model);
    }
}
