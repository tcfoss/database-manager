using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework.PseudoEntities;

[Keyless]
public record CreateFunctionEntity()
{
    [ExcludeFromCodeCoverage]
    [Column("Function")]
    public required string Function { get; set; }

    [Column("Create Function")]
    public string? CreateFunction { get; set; }
}
