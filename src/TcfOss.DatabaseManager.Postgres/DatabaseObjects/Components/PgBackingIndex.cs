using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgBackingIndex(Identifier? Name = null)
{
    public Identifier? Tablespace { get; init; }
    public uint? FillFactor { get; init; }
    public PgIndexBufferingMode? Buffering { get; init; }
    public bool? DeduplicateItems { get; init; }

    public PgConstraintIndexParameters? ToStatementIndexParameters()
    {
        SqlValueList<StatementIndexOption>? storage = PgIndex.GetStatementIndexOptions(FillFactor, Buffering, DeduplicateItems);
        return storage != null || Tablespace != null
            ? new PgConstraintIndexParameters(storage, Tablespace)
            : null;
    }
}
