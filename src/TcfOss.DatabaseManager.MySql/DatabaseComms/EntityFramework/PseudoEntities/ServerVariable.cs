using System.Diagnostics.CodeAnalysis;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework.PseudoEntities;

// ReSharper disable All
public record ServerVariable()
{
    [ExcludeFromCodeCoverage]
    public string? Variable_name { get; set; }
    public string? Value { get; set; }
}
