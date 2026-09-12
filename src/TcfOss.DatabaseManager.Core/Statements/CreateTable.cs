using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record CreateTable(ObjectName Name, SqlValueList<StatementColumn>? Columns, SqlValueList<StatementTableConstraint> Constraints) : Statement, ICreateStatement
{
    public bool OrReplace { get; init; }
    public bool Temporary { get; init; }
    public bool IfNotExists { get; init; }

    public bool? PrintAs { get; init; }
    public Select? AsSelect { get; init; }

    public SqlValueList<StatementTableOption> TableOptions { get; init; } = [];

    public override void ToSql(SqlTextWriter writer)
    {
        string? orReplace = OrReplace ? "OR REPLACE " : null;
        string? temporary = Temporary ? "TEMPORARY " : null;
        string? ifNotExists = IfNotExists ? "IF NOT EXISTS " : null;

        writer.WriteSql($"CREATE {orReplace}{temporary}TABLE {ifNotExists}{Name}");

        bool anyColumns = Columns.SafeAny();
        bool anyBody = anyColumns || Constraints.Any();
        if (anyBody)
        {
            writer.Write(" (");
        }
        if (anyColumns)
        {
            writer.WriteSql($"{Columns}");
        }
        if (Constraints.Any())
        {
            if (anyColumns)
            {
                writer.Write(", ");
            }
            writer.WriteSql($"{Constraints}");
        }
        if (anyBody)
        {
            writer.Write(")");
        }

        if (TableOptions.Any())
        {
            writer.Write(" ");
            writer.WriteDelimited(TableOptions, " ");
        }

        if (AsSelect != null)
        {
            if (PrintAs ?? false)
            {
                writer.Write(" AS");
            }
            writer.WriteSql($" {AsSelect}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        string? orReplace = OrReplace ? "OR REPLACE " : null;
        string? temporary = Temporary ? "TEMPORARY " : null;
        string? ifNotExists = IfNotExists ? "IF NOT EXISTS " : null;

        writer.WriteSql($"{manager.Indent}CREATE {orReplace}{temporary}TABLE {ifNotExists}");
        Name.FormatSql(writer, manager);

        if (Columns.SafeAny() || Constraints.Any())
        {
            CreateTableFormatter.FormatBody(writer, manager, Columns, Constraints);
        }

        if (TableOptions.Any())
        {
            CreateTableFormatter.FormatTableOptions(writer, manager, TableOptions);
        }

        if (AsSelect != null)
        {
            CreateTableFormatter.FormatAsSelect(writer, manager, AsSelect, PrintAs);
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (AsSelect != null)
        {
            foreach (ItemRef item in AsSelect.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
