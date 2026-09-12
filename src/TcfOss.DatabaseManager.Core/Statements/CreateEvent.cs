using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;

namespace TcfOss.DatabaseManager.Core.Statements;

public record CreateEvent(ObjectName Name, EventSchedule Schedule, Statement Body) : Statement, ICreateStatement, IHaveBodyStatement
{
    public CreateOrLabel? CreateOrLabel { get; init; }
    public Definer? Definer { get; init; }
    public bool IfNotExists { get; init; }
    public bool? OnCompletionPreserve { get; init; }
    public EventEnabledStatus? EnabledStatus { get; init; }
    public Comment? Comment { get; init; }

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
        writer.Write(" EVENT");
        if (IfNotExists)
        {
            writer.Write(" IF NOT EXISTS");
        }
        writer.WriteSql($" {Name} {Schedule}");
        if (OnCompletionPreserve == true)
        {
            writer.Write(" ON COMPLETION PRESERVE");
        }
        else if (OnCompletionPreserve == false)
        {
            writer.Write(" ON COMPLETION NOT PRESERVE");
        }
        if (EnabledStatus != null)
        {
            writer.WriteSql($" {EnabledStatus}");
        }
        if (Comment != null)
        {
            writer.WriteSql($" {Comment}");
        }
        if (options.PreferRawText && Meta.RawText != null)
        {
            writer.WriteSql($"{writer.NewLine}DO");
            if (!IWriteSql.StartsWithWhitespaceRegex.IsMatch(Meta.RawText))
            {
                writer.WriteLine();
            }
            writer.WriteSql($"{Meta.RawText}");
        }
        else
        {
            writer.WriteSql($" DO {Body}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        RoutineFormatter.FormatCreateAndDefiner(writer, manager, CreateOrLabel, Definer);
        writer.WriteLine();
        writer.WriteSqlI("EVENT ");
        if (IfNotExists)
        {
            writer.Write("IF NOT EXISTS ");
        }
        Name.FormatSql(writer, manager);
        manager.IncreaseIndent();
        writer.WriteLine();
        writer.WriteSqlI($"{Schedule}");
        if (OnCompletionPreserve == true)
        {
            writer.WriteLine();
            writer.WriteSqlI("ON COMPLETION PRESERVE");
        }
        else if (OnCompletionPreserve == false)
        {
            writer.WriteLine();
            writer.WriteSqlI("ON COMPLETION NOT PRESERVE");
        }
        if (EnabledStatus != null)
        {
            writer.WriteLine();
            writer.WriteSqlI($"{EnabledStatus}");
        }
        if (Comment != null)
        {
            writer.WriteLine();
            writer.WriteSqlI($"{Comment}");
        }
        manager.DecreaseIndent();
        writer.WriteLine();
        writer.WriteSqlI("DO");
        RoutineFormatter.FormatBody(writer, manager, Body);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Body.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
