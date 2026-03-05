namespace UchetNZP.Web.Configuration;

public class WarehouseDailyResetOptions
{
    /// <summary>
    /// Включает ежедневное обнуление склада в 18:00 по времени UTC+04:00.
    /// </summary>
    public bool Enabled { get; set; }
}
