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
    Statement Body) : Statement, ICreateStatement, IHaveBodyStatement, IWriteSqlWithOptions
{
    public Definer? Definer { get; init; }
    public TriggerOrder? Order { get; init; }
    public bool IfNotExists { get; init; }
    public CreateOrLabel? CreateOrLabel { get; init; }
    public bool TableNameBeforeTimeAndEvent { get; init; }
    public TriggerExecutionQuantifier? ExecutionQuantifier { get; init; }
    public bool TriggerBodyStartsWithAs { get; init; }
    public PgStatementTriggerOptions? PgOptions { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        ToSql(writer, new WriteOptions());
    }

    public void ToSql(SqlTextWriter writer, WriteOptions options)
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
        writer.Write(PgOptions?.ConstraintOptions != null ? " CONSTRAINT TRIGGER" : " TRIGGER");
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

        writer.WriteSql($" {TriggerTime} ");
        WriteEvents(writer);

        if (!TableNameBeforeTimeAndEvent)
        {
            // Other dialects (e.g. MySQL, PostgreSQL) put the trigger time and event before the table name
            writer.WriteSql($" ON {TableName}");
        }

        WritePgClauses(writer);

        if (ExecutionQuantifier != null)
        {
            writer.WriteSql($" {ExecutionQuantifier}");
        }

        if (PgOptions?.When != null)
        {
            writer.WriteSql($" WHEN ({PgOptions.When})");
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
        writer.WriteSqlI(PgOptions?.ConstraintOptions != null ? "CONSTRAINT TRIGGER " : "TRIGGER ");
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
            writer.WriteSqlI($"{TriggerTime} ");
            WriteEvents(writer);
        }
        else
        {
            writer.WriteSqlI($"{TriggerTime} ");
            WriteEvents(writer);
            writer.Write(" ON ");
            TableName.FormatSql(writer, manager);
        }

        WritePgClauses(writer);

        if (ExecutionQuantifier != null)
        {
            writer.WriteLine();
            ExecutionQuantifier.FormatSql(writer, manager);
        }

        if (PgOptions?.When != null)
        {
            writer.WriteLine();
            writer.WriteSqlI("WHEN (");
            PgOptions.When.FormatSql(writer, manager);
            writer.Write(")");
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

    private void WriteEvents(SqlTextWriter writer)
    {
        for (int index = 0; index < Events.Count; index++)
        {
            if (index > 0)
            {
                writer.Write(PgOptions?.EventsUseOr == true ? " OR " : ", ");
            }
            writer.WriteSql($"{Events[index]}");
        }
    }

    private void WritePgClauses(SqlTextWriter writer)
    {
        if (PgOptions?.ConstraintOptions is { } constraint)
        {
            if (constraint.FromTable != null)
            {
                writer.WriteSql($" FROM {constraint.FromTable}");
            }
            if (constraint.Deferrable != null)
            {
                writer.Write(constraint.Deferrable.Value ? " DEFERRABLE" : " NOT DEFERRABLE");
            }
            if (constraint.InitiallyDeferred != null)
            {
                writer.Write(constraint.InitiallyDeferred.Value ? " INITIALLY DEFERRED" : " INITIALLY IMMEDIATE");
            }
        }
        if (PgOptions?.TransitionRelations != null)
        {
            writer.WriteSql($" REFERENCING {PgOptions.TransitionRelations.ToSqlDelimited(" ")}");
        }
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
            if (PgOptions?.ConstraintOptions?.FromTable != null)
            {
                ItemRef? fromTable = context.CreateObjectRef(PgOptions.ConstraintOptions.FromTable);
                if (fromTable != null)
                {
                    yield return fromTable;
                }
            }
        }

        foreach (ItemRef item in Body.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
