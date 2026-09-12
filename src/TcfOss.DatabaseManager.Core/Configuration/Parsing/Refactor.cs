using TcfOss.DatabaseManager.Core.Configuration.Parsing.Attributes;

namespace TcfOss.DatabaseManager.Core.Configuration.Parsing;

/// <summary>
/// This class should include all fields that can be on any type of refactor
/// operation. It is used to make YAML deserialization easier.
/// </summary>
public class Refactor
{
    public required string UniqueId { get; set; }
    public required RefactorType Type { get; set; }
    public string? TableName { get; set; }
    public string? OldName { get; set; }
    public string? NewName { get; set; }
}
