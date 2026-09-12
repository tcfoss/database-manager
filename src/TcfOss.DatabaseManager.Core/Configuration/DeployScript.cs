using TcfOss.DatabaseManager.Core.DatabaseComms;

namespace TcfOss.DatabaseManager.Core.Configuration;

public record DeployScript
{
    public required DeployScriptType Type { get; init; }
    public required string FilePath { get; set; }
    public string FileName => Path.GetFileName(FilePath);
    public string? UniqueId { get; set; }
}
