using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework.PseudoEntities;

// ReSharper disable All
[Keyless]
public record CreateProcedureEntity()
{
    [ExcludeFromCodeCoverage]
    [Column("Procedure")]
    public required string Procedure { get; set; }

    [Column("Create Procedure")]
    public string? CreateProcedure { get; set; }
}
