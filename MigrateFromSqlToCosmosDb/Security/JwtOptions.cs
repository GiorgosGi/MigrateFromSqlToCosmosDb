namespace MigrateFromSqlToCosmosDb.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Authority { get; init; }
    public required string Audience { get; init; }
    public string RequiredScope { get; init; } = "trades.read";
    public string RequiredRole { get; init; } = "TradeReader";
}
