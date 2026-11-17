using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgConstraintOptions
{
    public bool Deferrable { get; init; }
    public bool InitiallyDeferred { get; init; }
    public bool NoInherit { get; init; }

    public SqlValueList<PgConstraintOption>? ToStatementOptions()
    {
        SqlValueList<PgConstraintOption> options = [];
        if (NoInherit)
        {
            options.Add(new PgConstraintOption.NoInherit());
        }
        if (Deferrable)
        {
            options.Add(new PgConstraintOption.Deferrable(true));
        }
        if (InitiallyDeferred)
        {
            options.Add(new PgConstraintOption.InitiallyDeferred(true));
        }
        return options.Count == 0 ? null : options;
    }
}
