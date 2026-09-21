using TcfOss.DatabaseManager.Core.DatabaseComms;

namespace TcfOss.DatabaseManager.Core.Configuration;

public record DeployScript
{
    public required DeployScriptType Type { get; init; }
    public required FileInfo FilePath { get; set; }
    public string FileName => FilePath.Name;
    public string? UniqueId { get; set; }
}
