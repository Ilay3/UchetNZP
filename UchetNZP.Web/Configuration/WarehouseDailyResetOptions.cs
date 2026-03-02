namespace UchetNZP.Web.Configuration;

public class WarehouseDailyResetOptions
{
    /// <summary>
    /// Включает ежедневное обнуление склада в 00:00 по локальному времени сервера.
    /// </summary>
    public bool Enabled { get; set; }
}
