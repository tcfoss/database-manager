using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework;

public static class EntityFrameworkExtensions
{
    // ReSharper disable once UnusedMethodReturnValue.Global
    public static EntityTypeBuilder<T> VarcharProperty<T>(this EntityTypeBuilder<T> builder, Expression<Func<T, string?>> selector, string columnName, int maxLength, bool isRequired = false) where T : class
    {
        builder.Property(selector)
            .HasColumnType("varchar")
            .HasColumnName(columnName)
            .HasMaxLength(maxLength)
            .IsRequired(isRequired);

        return builder;
    }
}
