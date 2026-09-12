using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;

namespace TcfOss.DatabaseManager.Core.Statements;

public record CreateView(ObjectName Name, Select Body) : Statement, ICreateStatement
{
    public SqlValueList<Identifier>? Columns { get; init; }
    public CreateOrLabel? CreateOrLabel { get; init; }
    public bool IfNotExists { get; init; }
    public Definer? Definer { get; init; }
    public ViewAlgorithm? ViewAlgorithm { get; init; }
    public SecurityContext? SecurityContext { get; init; }
    public ViewCheckOption? ViewCheckOption { get; init; }
    public MsViewWithOptions? MsOptions { get; init; }

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
        if (ViewAlgorithm != null)
        {
            writer.WriteSql($" ALGORITHM = {ViewAlgorithm}");
        }
        if (Definer != null)
        {
            writer.WriteSql($" {Definer}");
        }
        if (SecurityContext != null)
        {
            writer.WriteSql($" SQL SECURITY {SecurityContext}");
        }
        writer.Write(" VIEW");
        if (IfNotExists)
        {
            writer.Write(" IF NOT EXISTS");
        }
        writer.WriteSql($" {Name}");
        if (Columns != null)
        {
            writer.WriteSql($" ({Columns})");
        }
        if (MsOptions != null)
        {
            writer.WriteSql($" {MsOptions}");
        }
        if (options.PreferRawText && Meta.RawText != null)
        {
            writer.WriteLine();
            writer.Write("AS");
            if (!IWriteSql.StartsWithWhitespaceRegex.IsMatch(Meta.RawText))
            {
                writer.WriteLine();
            }
            writer.WriteSql($"{Meta.RawText}");
        }
        else
        {

            writer.WriteSql($" AS {Body}");

            if (ViewCheckOption != null)
            {
                writer.WriteSql($" WITH {ViewCheckOption} CHECK OPTION");
            }
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("CREATE");
        if (CreateOrLabel != null)
        {
            writer.WriteSql($" {CreateOrLabel}");
        }
        // Indented modifiers: ALGORITHM, DEFINER, SQL SECURITY
        bool hasModifiers = ViewAlgorithm != null || Definer != null || SecurityContext != null;
        if (hasModifiers)
        {
            manager.IncreaseIndent();
            if (ViewAlgorithm != null)
            {
                writer.WriteLine();
                writer.WriteSqlI($"ALGORITHM = {ViewAlgorithm}");
            }
            if (Definer != null)
            {
                writer.WriteLine();
                writer.WriteSqlI($"{Definer}");
            }
            if (SecurityContext != null)
            {
                writer.WriteLine();
                writer.WriteSqlI($"SQL SECURITY {SecurityContext}");
            }
            manager.DecreaseIndent();
        }
        // VIEW [IF NOT EXISTS] name [(columns)]
        writer.WriteLine();
        writer.WriteSqlI("VIEW ");
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
        if (MsOptions != null)
        {
            writer.WriteLine();
            writer.WriteSqlI($"{MsOptions}");
        }
        // AS
        writer.WriteLine();
        writer.WriteSqlI("AS");
        // Body (Select)
        writer.WriteLine();
        Body.FormatSql(writer, manager);
        // WITH CHECK OPTION
        if (ViewCheckOption != null)
        {
            writer.WriteLine();
            writer.WriteSqlI($"WITH {ViewCheckOption} CHECK OPTION");
        }
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
