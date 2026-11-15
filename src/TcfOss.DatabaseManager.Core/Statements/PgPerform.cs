using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record PgPerform : SimpleSelect
{
    public PgPerform(SqlValueList<SimpleSelectItem> selections) : base(selections) { }

    internal PgPerform(SimpleSelect query) : base(query) { }

    protected override string QueryKeyword => "PERFORM";
}
