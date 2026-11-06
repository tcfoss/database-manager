using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public record PgIdentityOptions(long Start, long Increment, long MinValue, long MaxValue, long Cache, bool Cycle)
{
    public SqlValueList<PgStatementSequenceOption> ToStatementOptions()
    {
        return
        [
            new PgStatementSequenceOption.Start(Start) { UseWith = true },
            new PgStatementSequenceOption.Increment(Increment) { UseBy = true },
            new PgStatementSequenceOption.MinValue(MinValue),
            new PgStatementSequenceOption.MaxValue(MaxValue),
            new PgStatementSequenceOption.Cache(Cache),
            new PgStatementSequenceOption.Cycle(Cycle)
        ];
    }
}
