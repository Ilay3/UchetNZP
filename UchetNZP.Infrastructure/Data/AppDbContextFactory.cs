using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UchetNZP.Infrastructure.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DefaultConnection = "Host=localhost;Port=5432;Database=UchetNZP;Username=postgres;Password=postgres";

    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(DefaultConnection);

        return new AppDbContext(optionsBuilder.Options);
    }
}
