using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements.PgDeclarations;

public record SubVariableDeclaration(Identifier Name, DataTypeReference DataType)
    : Statement
{
    public bool Constant { get; init; }
    public ObjectName? Collation { get; init; }
    public bool NotNull { get; init; }
    public PgBlockVariableDefault? Default { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Name}");
        if (Constant)
        {
            writer.Write(" CONSTANT");
        }
        writer.WriteSql($" {DataType}");
        if (Collation != null)
        {
            writer.WriteSql($" COLLATE {Collation}");
        }
        if (NotNull)
        {
            writer.Write(" NOT NULL");
        }
        if (Default != null)
        {
            writer.WriteSql($" {Default}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        manager.WriteBlockPart(writer, "");
        Name.FormatSql(writer, manager);
        if (Constant)
        {
            writer.Write(" CONSTANT");
        }
        writer.Write(" ");
        DataType.FormatSql(writer, manager);
        if (Collation != null)
        {
            writer.Write(" COLLATE ");
            Collation.FormatSql(writer, manager);
        }
        if (NotNull)
        {
            writer.Write(" NOT NULL");
        }
        if (Default != null)
        {
            writer.Write(" ");
            Default.FormatSql(writer, manager);
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        return Default?.GetReferencedItems(context) ?? [];
    }
}
