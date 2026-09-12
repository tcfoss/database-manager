using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace TcfOss.DatabaseManager.MsSql.DatabaseComms.EntityFramework;

public class SysContext(DbContextOptions<SysContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
