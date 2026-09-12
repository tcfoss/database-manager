using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;

namespace TcfOss.DatabaseManager.Core.Statements;

public record CreateProcedure(ObjectName Name, SqlValueList<RoutineParameter> Parameters, Statement Body) : Statement, ICreateStatement, IHaveBodyStatement
{
    public CreateOrLabel? CreateOrLabel { get; init; }
    public bool IfNotExists { get; init; }
    public Definer? Definer { get; init; }
    public MyRoutineCharacteristic? MyCharacteristic { get; init; }
    public MsRoutineWithOptions? MsOptions { get; init; }

    /// <summary>If true, emit <c>AS</c> between the routine header and body
    /// (T-SQL syntax). MySQL/MariaDB use no separator and leave this false.</summary>
    public bool UseAsBeforeBody { get; init; }

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
        writer.Write(" PROCEDURE");
        if (IfNotExists)
        {
            writer.Write(" IF NOT EXISTS");
        }
        writer.WriteSql($" {Name} ({Parameters})");
        if (MyCharacteristic != null)
        {
            writer.WriteSql($" {MyCharacteristic}");
        }
        if (MsOptions != null)
        {
            writer.WriteSql($" {MsOptions}");
        }
        if (options.PreferRawText && Meta.RawText != null)
        {
            if (!IWriteSql.StartsWithWhitespaceRegex.IsMatch(Meta.RawText))
            {
                writer.WriteLine();
            }
            writer.WriteSql($"{Meta.RawText}");
        }
        else
        {
            writer.Write(UseAsBeforeBody ? " AS " : " ");
            writer.WriteSql($"{Body}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        RoutineFormatter.FormatHeader(writer, manager, CreateOrLabel, Definer, "PROCEDURE", Name, Parameters, IfNotExists, null, MyCharacteristic, MsOptions);
        RoutineFormatter.FormatBody(writer, manager, Body, UseAsBeforeBody);
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
