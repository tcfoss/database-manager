using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record CreateMaterializedView(ObjectName Name, Select Body) : Statement, ICreateStatement
{
    public SqlValueList<Identifier>? Columns { get; init; }
    public bool IfNotExists { get; init; }
    public Identifier? AccessMethod { get; init; }
    public SqlValueList<PgStorageParameter>? StorageParameters { get; init; }
    public Identifier? Tablespace { get; init; }
    public bool? WithData { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("CREATE MATERIALIZED VIEW");
        if (IfNotExists)
        {
            writer.Write(" IF NOT EXISTS");
        }
        writer.WriteSql($" {Name}");
        if (Columns != null)
        {
            writer.WriteSql($" ({Columns})");
        }
        if (AccessMethod != null)
        {
            writer.WriteSql($" USING {AccessMethod}");
        }
        if (StorageParameters != null)
        {
            writer.WriteSql($" WITH ({StorageParameters})");
        }
        if (Tablespace != null)
        {
            writer.WriteSql($" TABLESPACE {Tablespace}");
        }
        writer.WriteSql($" AS {Body}");
        if (WithData != null)
        {
            writer.Write(WithData.Value ? " WITH DATA" : " WITH NO DATA");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("CREATE MATERIALIZED VIEW ");
        if (IfNotExists)
        {
            writer.Write("IF NOT EXISTS ");
        }
        Name.FormatSql(writer, manager);
        if (Columns != null)
        {
            writer.Write(" (");
            writer.FormatDelimited(Columns, manager);
            writer.Write(")");
        }
        if (AccessMethod != null)
        {
            writer.Write(" USING ");
            AccessMethod.FormatSql(writer, manager);
        }
        if (StorageParameters != null)
        {
            writer.WriteLine();
            writer.WriteSqlI("WITH (");
            writer.FormatDelimited(StorageParameters, manager);
            writer.Write(")");
        }
        if (Tablespace != null)
        {
            writer.WriteLine();
            writer.WriteSqlI("TABLESPACE ");
            Tablespace.FormatSql(writer, manager);
        }
        writer.WriteLine();
        writer.WriteSqlI("AS");
        if (!Body.StartsWithNewLines)
        {
            writer.WriteLine();
        }
        Body.FormatSql(writer, manager);
        if (WithData != null)
        {
            if (writer.GetCurrentLineLength() > 0)
            {
                writer.WriteLine();
            }
            writer.WriteSqlI(WithData.Value ? "WITH DATA" : "WITH NO DATA");
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        return Body.GetReferencedItems(context);
    }
}
