using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Formatting;

public class FormatManager
{

    public required FormattingSettings Formatting { get; init; }

    public required IFunctionNameProvider FunctionNameProvider { get; init; }
    public required ComponentNormalizer ComponentNormalizer { get; init; }
    public SchemaIdentifier? ActiveSchema { get; set; }
    public PseudoTableSet? PseudoTables { get; set; }

    private readonly Stack<FormatContext> _contextStack = new();
    public bool ExpectingQualifiedColumn { get; private set; }

    public bool ExpectingNewLineBeforeStatement { get; set; }

    public bool SelectItemsFirst { get; set; }
    public bool SelectItemsLast { get; set; }

    public required Indenter Indenter { get; init; }
    public string Indent => Indenter.IndentString;
    public void IncreaseIndent(int? spaces = null)
    {
        Indenter.IncreaseIndent(spaces);
    }
    public void DecreaseIndent(int? spaces = null)
    {
        Indenter.DecreaseIndent(spaces);
    }

    public void TerminateStatement(SqlTextWriter writer, Statement statement)
    {
        if (statement is not InertOnly)
        {
            writer.TrimEnd();
            writer.Write(";");
        }
        if (!writer.EndWithNewLine())
        {
            ExpectingNewLineBeforeStatement = true;
        }
    }

    public void WriteBlockPart(SqlTextWriter writer, string text, bool indent = true)
    {
        if (ExpectingNewLineBeforeStatement)
        {
            writer.WriteLine();
        }
        if (indent)
        {
            writer.Write(Indent);
        }
        writer.Write(text);
        ExpectingNewLineBeforeStatement = true;
    }

    public SqlValueList<Identifier>? GetPseudoTable(bool includeSchema, params Identifier[] identifiers)
    {
        if (PseudoTables == null)
        {
            return null;
        }

        PseudoTable[] tables = PseudoTables.GetPseudoTables(
            tableName: identifiers.Last().Name,
            schemaName: identifiers.Length >= 2 ? identifiers[^2].Name : null,
            catalogName: identifiers.Length == 3 ? identifiers[0].Name : null);

        PseudoTable table;
        if (tables.Length == 1)
        {
            table = tables[0];
        }
        else
        {
            return null;
        }

        if (table.Identifier != null && includeSchema)
        {
            return [table.Identifier!.Schema.ToSimpleIdentifier(), table.Identifier!.ToSimpleIdentifier()];
        }
        return [new Identifier(table.Alias ?? table.Name)];
    }

    public SqlValueList<Identifier>? GetQualifiedSelectableItem(params Identifier[] identifiers)
    {
        if (PseudoTables == null)
        {
            return null;
        }

        PseudoTableSet.SelectableItem[] items = PseudoTables.GetSelectableElements(
            name: identifiers.Last().Name,
            sourceName: identifiers.Length >= 2 ? identifiers[^2].Name : null,
            sourceParentName: identifiers.Length == 3 ? identifiers[0].Name : null);

        PseudoTableSet.SelectableItem item;
        if (items.Length == 1)
        {
            item = items[0];
        }
        else
        {
            return null;
        }

        return [new Identifier(item.Source), new Identifier(item.Name)];
    }

    public Identifier GetQuotedIdentifier(Identifier given)
    {
        return ComponentNormalizer.NormalizeSingleIdentifier(given, Formatting.Quoting);
    }

    public Identifier[] QuoteIdentifiers(IReadOnlyList<Identifier> given, IReadOnlyList<Identifier> actual)
    {
        var result = new Identifier[actual.Count];

        int iActual = actual.Count - 1;
        int iGiven = given.Count - 1;

        while (iActual >= 0)
        {
            Identifier curr;
            if (iGiven >= 0)
            {
                curr = actual[iActual] with { QuoteStyle = given[iGiven].QuoteStyle };
            }
            else
            {
                curr = actual[iActual];
            }
            result[iActual] = ComponentNormalizer.NormalizeSingleIdentifier(curr, Formatting.Quoting);
            iActual--;
            iGiven--;
        }
        return result;
    }

    public ContextScope Enter(FormatContext context)
    {
        return new ContextScope(this, context);
    }


    public readonly struct ContextScope : IDisposable
    {
        private readonly FormatManager _manager;
        private readonly bool _previousExpectingQualifiedColumn;

        public ContextScope(FormatManager manager, FormatContext context)
        {
            _manager = manager;
            _manager._contextStack.Push(context);
            _previousExpectingQualifiedColumn = _manager.ExpectingQualifiedColumn;
            // Consider inheriting ExpectingQualifiedColumn when entering contexts that
            // are neutral with respect to it. Will require a more careful implementation of
            // GetExpectingQualifiedColumn, probably returnning `bool?` for contexts
            // that don't care.
            _manager.ExpectingQualifiedColumn = GetExpectingQualifiedColumn(context);
        }

        public bool InContext(FormatContext context)
        {
            foreach (FormatContext ctx in _manager._contextStack)
            {
                if (ctx == context)
                {
                    return true;
                }
            }
            return false;
        }

        private bool GetExpectingQualifiedColumn(FormatContext? topContext)
        {
            topContext ??= _manager._contextStack.Peek();

            if (topContext == FormatContext.SelectItem
                || topContext == FormatContext.JoinCondition
                || topContext == FormatContext.WhereClause
                || topContext == FormatContext.OrderByClause
                || topContext == FormatContext.GroupByClause
                || topContext == FormatContext.HavingClause
            )
            {
                return _manager.Formatting.SelectItemPrefixWithObject;
            }
            else if (topContext == FormatContext.AssignmentTarget)
            {
                if (InContext(FormatContext.UpdateSetBlock) && _manager.Formatting.UpdateTargetPrefixWithObject)
                {
                    return true;
                }
                if (InContext(FormatContext.InsertOnDuplicateUpdate) && _manager.Formatting.InsertUpdateTargetPrefixWithObject)
                {
                    return true;
                }
            }
            else if (topContext == FormatContext.AssignmentValue)
            {
                if (InContext(FormatContext.UpdateSetBlock) && _manager.Formatting.UpdateSourcePrefixWithObject)
                {
                    return true;
                }
                if (InContext(FormatContext.InsertOnDuplicateUpdate) && _manager.Formatting.InsertUpdateSourcePrefixWithObject)
                {
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            _manager._contextStack.Pop();
            _manager.ExpectingQualifiedColumn = _previousExpectingQualifiedColumn;
        }
    }
}
