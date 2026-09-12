using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;

namespace TcfOss.DatabaseManager.Core.Statements;

public record CreateTrigger(
    ObjectName Name,
    TriggerTime TriggerTime,
    SqlValueList<TriggerEvent> Events,
    ObjectName TableName,
    Statement Body) : Statement, ICreateStatement, IHaveBodyStatement
{
    public Definer? Definer { get; init; }
    public TriggerOrder? Order { get; init; }
    public bool IfNotExists { get; init; }
    public CreateOrLabel? CreateOrLabel { get; init; }
    public bool TableNameBeforeTimeAndEvent { get; init; }
    public TriggerExecutionQuantifier? ExecutionQuantifier { get; init; }
    public bool TriggerBodyStartsWithAs { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        ToSql(writer, IWriteSql.DefaultWriteOptions);
    }

    public override void ToSql(SqlTextWriter writer, WriteOptions options)
    {
        writer.Write("CREATE");
        if (CreateOrLabel != null)
        {
            writer.WriteSql($" {CreateOrLabel}");
        }
        if (Definer != null)
        {
            writer.WriteSql($" {Definer}");
        }
        writer.Write(" TRIGGER");
        if (IfNotExists)
        {
            writer.Write(" IF NOT EXISTS");
        }
        writer.WriteSql($" {Name}");

        if (TableNameBeforeTimeAndEvent)
        {
            // Some dialects (e.g. T-SQL) put the table name before the trigger time and event
            writer.WriteSql($" ON {TableName}");
        }

        writer.WriteSql($" {TriggerTime} {Events}");

        if (!TableNameBeforeTimeAndEvent)
        {
            // Other dialects (e.g. MySQL, PostgreSQL) put the trigger time and event before the table name
            writer.WriteSql($" ON {TableName}");
        }

        if (ExecutionQuantifier != null)
        {
            writer.WriteSql($" {ExecutionQuantifier}");
        }

        if (Order != null)
        {
            writer.WriteSql($" {Order}");
        }

        if (options.PreferRawText && Meta.RawText != null)
        {
            if (!IWriteSql.StartsWithWhitespaceRegex.IsMatch(Meta.RawText))
            {
                writer.WriteLine();
            }
            writer.WriteSql($"{Meta.RawText}");
        }
        else if (TriggerBodyStartsWithAs)
        {
            writer.WriteSql($" AS {Body}");
        }
        else
        {
            writer.WriteSql($" {Body}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        RoutineFormatter.FormatCreateAndDefiner(writer, manager, CreateOrLabel, Definer);
        writer.WriteLine();
        writer.WriteSqlI("TRIGGER ");
        if (IfNotExists)
        {
            writer.Write("IF NOT EXISTS ");
        }
        Name.FormatSql(writer, manager);
        writer.WriteLine();

        if (TableNameBeforeTimeAndEvent)
        {
            writer.WriteSqlI("ON ");
            TableName.FormatSql(writer, manager);
            writer.WriteLine();
            writer.WriteSqlI($"{TriggerTime} {Events}");
        }
        else
        {
            writer.WriteSqlI($"{TriggerTime} {Events} ON ");
            TableName.FormatSql(writer, manager);
        }

        if (ExecutionQuantifier != null)
        {
            writer.WriteLine();
            ExecutionQuantifier.FormatSql(writer, manager);
        }

        if (Order != null)
        {
            writer.WriteLine();
            manager.IncreaseIndent();
            writer.WriteSqlI($"{Order.Position} ");
            Order.OtherTrigger.FormatSql(writer, manager);
            manager.DecreaseIndent();
        }

        if (TriggerBodyStartsWithAs)
        {
            writer.WriteLine();
            writer.WriteSqlI("AS ");
        }

        RoutineFormatter.FormatBody(writer, manager, Body);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using (context.Enter(ReferencedItemsContext.CreateTriggerTable))
        {
            ItemRef? item = context.CreateObjectRef(TableName);
            if (item != null)
            {
                yield return item;
            }
        }

        foreach (ItemRef item in Body.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
