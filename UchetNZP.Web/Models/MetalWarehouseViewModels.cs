using System.Globalization;

namespace UchetNZP.Web.Models;

public class MetalWarehouseDashboardViewModel
{
    public int MaterialsInCatalog { get; init; }

    public decimal MetalUnitsInStock { get; init; }

    public int OpenRequirements { get; init; }

    public int MovementsToday { get; init; }

    public bool HasAnyData => MaterialsInCatalog > 0 || MetalUnitsInStock > 0 || OpenRequirements > 0 || MovementsToday > 0;

    public string MetalUnitsInStockDisplay => MetalUnitsInStock.ToString("0.###", CultureInfo.CurrentCulture);
}

public class MetalWarehouseListPageViewModel
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Headers { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<IReadOnlyCollection<string>> Rows { get; init; } = Array.Empty<IReadOnlyCollection<string>>();

    public string EmptyStateTitle { get; init; } = "Пока пусто";

    public string EmptyStateDescription { get; init; } = "Данные появятся после запуска следующих этапов модуля.";
}
