using Microsoft.EntityFrameworkCore;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Part> Parts => Set<Part>();

    public DbSet<Section> Sections => Set<Section>();

    public DbSet<Operation> Operations => Set<Operation>();

    public DbSet<PartRoute> PartRoutes => Set<PartRoute>();

    public DbSet<WipBalance> WipBalances => Set<WipBalance>();

    public DbSet<WipReceipt> WipReceipts => Set<WipReceipt>();

    public DbSet<WipLaunch> WipLaunches => Set<WipLaunch>();

    public DbSet<WipLaunchOperation> WipLaunchOperations => Set<WipLaunchOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
