using TcfOss.DatabaseManager.Core.DatabaseComms;

namespace TcfOss.DatabaseManager.Core.Configuration.Parsing;

public record DeployScript
{
    public required string FilePath { get; init; }
    public DeployScriptType? Type { get; init; }
    public string? UniqueId { get; init; }
}
