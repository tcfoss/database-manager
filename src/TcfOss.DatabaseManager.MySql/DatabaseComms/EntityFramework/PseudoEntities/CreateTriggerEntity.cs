using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework.PseudoEntities;

// ReSharper disable All
[Keyless]
public record CreateTriggerEntity()
{
    [ExcludeFromCodeCoverage]
    [Column("Trigger")]
    public required string Trigger { get; set; }

    [Column("SQL Original Statement")]
    public string? CreateTrigger { get; set; }
}
