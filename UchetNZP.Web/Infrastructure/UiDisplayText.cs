namespace UchetNZP.Web.Infrastructure;

public static class UiDisplayText
{
    public static string Status(string? status)
    {
        return Normalize(status) switch
        {
            "active" => "Активно",
            "calculated" => "Рассчитан",
            "cancelled" => "Отменено",
            "closed" => "Закрыто",
            "completed" => "Завершено",
            "consumed" => "Израсходовано",
            "created" => "Создано",
            "deficit" => "Дефицит",
            "draft" => "Черновик",
            "fulluse" => "Полное использование",
            "hasdeficit" => "Есть дефицит",
            "merged" => "Объединен",
            "partialcut" => "Частичное использование",
            "partiallyused" => "Частично использовано",
            "planned" => "Запланировано",
            "readytoissue" => "Готово к выдаче",
            "reservecandidate" => "Резерв",
            "reverted" => "Отменено",
            "scrapped" => "Брак",
            "updated" => "Обновлено",
            "вналичии" => "В наличии",
            "недоступно" => "Недоступно",
            "израсходовано" => "Израсходовано",
            "частичноиспользовано" => "Частично использовано",
            "активно" => "Активно",
            "отменено" => "Отменено",
            _ => string.IsNullOrWhiteSpace(status) ? "—" : status.Trim(),
        };
    }

    public static string MovementType(string? movementType)
    {
        return Normalize(movementType) switch
        {
            "receipt" => "Приход",
            "issue" => "Расход",
            "fullconsumption" => "Полное списание",
            "residualupdate" => "Коррекция остатка",
            _ => string.IsNullOrWhiteSpace(movementType) ? "—" : movementType.Trim(),
        };
    }

    public static string StatusClass(string? status)
    {
        return Normalize(status) switch
        {
            "active" or "completed" or "created" or "calculated" or "вналичии" or "активно" => "app-status app-status--success",
            "readytoissue" or "planned" or "updated" or "reservecandidate" => "app-status app-status--info",
            "draft" or "partialcut" or "partiallyused" or "частичноиспользовано" => "app-status app-status--warning",
            "cancelled" or "closed" or "consumed" or "deficit" or "hasdeficit" or "reverted" or "scrapped" or "недоступно" or "израсходовано" or "отменено" => "app-status app-status--danger",
            _ => "app-status app-status--neutral",
        };
    }

    public static string MovementClass(string? movementType)
    {
        return Normalize(movementType) switch
        {
            "receipt" or "residualupdate" => "app-status app-status--success",
            "issue" or "fullconsumption" => "app-status app-status--warning",
            _ => "app-status app-status--neutral",
        };
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant().Replace(" ", string.Empty, StringComparison.Ordinal);
    }
}
