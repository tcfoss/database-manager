using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgColumn(ColumnIdentifier Name, DataType DataType)
{
    public ObjectName? Collation { get; init; }
    public ColumnOption.Nullability? Nullability { get; init; }
    public PgConstraintOptions NullabilityOptions { get; init; } = new();
    public ColumnOption.ColumnDefault? Default { get; init; }
    public ColumnOption.Generated.AsExpression? Generated { get; init; }
    public PgIdentity? Identity { get; init; }
    public PgColumnStorageMode? Storage { get; init; }
    public PgCompressionMethod Compression { get; init; } = PgCompressionMethod.Default;

    public StatementColumn.Typed ToStatementColumn(DifferFormatManager? manager = null)
    {
        SqlValueList<StatementColumnOption> options = [];
        if (Storage != null)
        {
            options.Add(new StatementColumnOption.PgStorage(Storage));
        }
        if (Compression != PgCompressionMethod.Default)
        {
            options.Add(new StatementColumnOption.PgCompression(Compression));
        }
        if (Collation != null)
        {
            options.Add(new StatementColumnOption.PgCollation(Collation));
        }
        if (Nullability is ColumnOption.Nullability.Null nullable)
        {
            options.Add(new StatementColumnOption.Nullability.Null(nullable.Name));
        }
        else if (Nullability is ColumnOption.Nullability.NotNull notNull)
        {
            options.Add(new StatementColumnOption.Nullability.NotNull(notNull.Name)
            {
                PgOptions = NullabilityOptions.ToStatementOptions()
            });
        }

        if (Default is ColumnOption.ColumnDefault.DefaultValue value)
        {
            options.Add(new StatementColumnOption.Default.DefaultValue(value.Value, value.Name));
        }
        else if (Default is ColumnOption.ColumnDefault.DefaultExpression expression)
        {
            options.Add(new StatementColumnOption.Default.DefaultExpression(expression.Expression, expression.Name));
        }

        if (Generated is ColumnOption.Generated.AsExpression generated)
        {
            options.Add(new StatementColumnOption.Generated.AsExpression(generated.Expression, generated.Mode, true, true, false));
        }
        else if (Identity is { } identity)
        {
            SqlValueList<PgStatementSequenceOption> sequenceOptions = identity.BackingSequence.Options?.ToStatementOptions() ?? [];
            if (identity.BackingSequence.Name is { } sequenceName)
            {
                sequenceOptions.Add(new PgStatementSequenceOption.SequenceName(sequenceName.ToObjectName(2)));
            }
            options.Add(new StatementColumnOption.Generated.PgIdentity(identity.Always, sequenceOptions.Count == 0 ? null : sequenceOptions));
        }

        return new StatementColumn.Typed(Name.ToSimpleIdentifier(),
            manager != null ? DataType.ToDataTypeForTable(manager) : DataType, options);
    }
}
