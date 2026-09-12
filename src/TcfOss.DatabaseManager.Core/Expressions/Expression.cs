using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Expressions;

public abstract record Expression() : IWriteSql
{
    public NonSql? PreNonSql { get; init; }

    public abstract void ToSql(SqlTextWriter writer);
    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public abstract IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);

    internal INegated AsNegated
    {
        get
        {
            return (INegated)this;
        }
    }
}
