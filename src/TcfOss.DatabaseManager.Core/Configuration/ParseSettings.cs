namespace TcfOss.DatabaseManager.Core.Configuration;

public record ParseSettings
{
    public bool RemoveSlashesBeforeQuotesGenerationExpression { get; init; }
    public bool RemoveSlashesBeforeQuotesColumnDefault { get; init; }
}
