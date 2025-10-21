namespace UchetNZP.Web.Configuration;

public class MaintenanceOptions
{
    /// <summary>
    /// Разрешает выполнение скрытой команды очистки базы данных.
    /// </summary>
    public bool AllowClearDatabaseEndpoint { get; set; }
}
