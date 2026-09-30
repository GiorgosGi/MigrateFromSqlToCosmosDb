using Microsoft.EntityFrameworkCore;

namespace MigrateFromSqlToCosmosDb.Infrastructure.SqlServer;

public sealed class TradeSourceDbContext(DbContextOptions<TradeSourceDbContext> options) : DbContext(options)
{
    public DbSet<TradeSourceRow> Trades => Set<TradeSourceRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TradeSourceRow>(entity =>
        {
            entity.ToTable("Trades", "dbo");
            entity.HasKey(trade => trade.Id);
            entity.Property(trade => trade.AccountId).HasMaxLength(128);
            entity.Property(trade => trade.Instrument).HasMaxLength(64);
            entity.Property(trade => trade.Price).HasPrecision(18, 6);
            entity.Property(trade => trade.Quantity).HasPrecision(18, 6);
            entity.Property(trade => trade.Status).HasMaxLength(32);
        });
    }
}

public sealed class TradeSourceRow
{
    public long Id { get; init; }
    public required string AccountId { get; init; }
    public required string Instrument { get; init; }
    public decimal Quantity { get; init; }
    public decimal Price { get; init; }
    public DateTimeOffset ExecutedAt { get; init; }
    public required string Status { get; init; }
}
