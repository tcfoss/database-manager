using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgIdentityOptions(
    long? Start = null,
    long? Increment = null,
    PgIdentityOptions.Bound? MinValue = null,
    PgIdentityOptions.Bound? MaxValue = null,
    long? Cache = null,
    bool? Cycle = null)
{
    public record Bound(long? Value);

    public SqlValueList<PgStatementSequenceOption> ToStatementOptions()
    {
        SqlValueList<PgStatementSequenceOption> options = [];
        if (Start is { } start)
        {
            options.Add(new PgStatementSequenceOption.Start(start) { UseWith = true });
        }
        if (Increment is { } increment)
        {
            options.Add(new PgStatementSequenceOption.Increment(increment) { UseBy = true });
        }
        if (MinValue is { } minimum)
        {
            options.Add(new PgStatementSequenceOption.MinValue(minimum.Value));
        }
        if (MaxValue is { } maximum)
        {
            options.Add(new PgStatementSequenceOption.MaxValue(maximum.Value));
        }
        if (Cache is { } cache)
        {
            options.Add(new PgStatementSequenceOption.Cache(cache));
        }
        if (Cycle is { } cycle)
        {
            options.Add(new PgStatementSequenceOption.Cycle(cycle));
        }
        return options;
    }
}
