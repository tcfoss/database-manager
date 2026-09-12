namespace TcfOss.DatabaseManager.Core.Configuration;

public record SchemaDefaults()
{
    public required string Engine { get; init; }
    public required string CharacterSet { get; init; }
    public required string Collation { get; init; }
}
