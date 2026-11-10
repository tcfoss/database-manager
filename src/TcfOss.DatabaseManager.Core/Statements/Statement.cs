using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record Statement : IWriteSql, IHaveMeta, IReferenceItems
{
    public MetaData Meta { get; init; } = new();

    public abstract void ToSql(SqlTextWriter writer);

    public abstract IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);

    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.Write(manager.Indent);
        ToSql(writer);
        Meta.FormatPostNonSql(writer, manager);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public bool StartsWithNewLines => Meta.PreNonSql?.OfType<NonSql.Newlines>().Any() ?? false;
}
