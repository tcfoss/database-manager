using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework.PseudoEntities;

// ReSharper disable All
[Keyless]
public record CreateViewEntity()
{
    [ExcludeFromCodeCoverage]
    [Column("View")]
    public required string View { get; set; }

    [Column("Create View")]
    public string? CreateView { get; set; }
}
