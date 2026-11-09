using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record TableFactor() : IWriteSql, IAddTablesToContext
{
    public Identifier? Alias { get; init; }
    public SqlValueList<Identifier>? AliasColumns { get; init; }
    public bool IsLateral { get; init; }

    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);
    public abstract void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null);
    public abstract IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);

    private void WriteLateral(SqlTextWriter writer)
    {
        if (IsLateral)
        {
            writer.Write("LATERAL ");
        }
    }

    private void WriteAlias(SqlTextWriter writer)
    {
        if (Alias != null)
        {
            writer.WriteSql($" AS {Alias}");
            if (AliasColumns != null)
            {
                writer.WriteSql($" ({AliasColumns.ToSqlDelimited()})");
            }
        }
    }

    private void FormatAlias(SqlTextWriter writer, FormatManager manager, bool quoteAlias = true)
    {
        if (Alias != null)
        {
            Identifier alias = quoteAlias ? manager.GetQuotedIdentifier(Alias) : Alias;
            writer.WriteSql($" AS {alias}");
            if (AliasColumns != null)
            {
                writer.Write(" (");
                writer.WriteDelimited(AliasColumns.Select(manager.GetQuotedIdentifier), ", ");
                writer.Write(")");
            }
        }
    }

    private void AddFunctionSource(PseudoTableSet pseudoTableSet, string defaultName)
    {
        string sourceName = Alias?.Name ?? defaultName;
        PseudoTable source = new PseudoTable.Unbound(sourceName, null, PseudoTableType.Table) { Alias = Alias?.Name }.RenameColumns(AliasColumns);
        pseudoTableSet.AddLocalSource(sourceName, source, Alias?.Name);
    }

    public record Table(ObjectName Name) : TableFactor
    {
        /// <summary>
        /// Provider-specific table hints (T-SQL <c>WITH (NOLOCK, ...)</c>).
        /// Null for non-T-SQL dialects.
        /// </summary>
        public SqlValueList<TableHint>? Hints { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            WriteLateral(writer);
            writer.WriteSql($"{Name}");
            WriteAlias(writer);
            if (Hints != null)
            {
                writer.WriteSql($" WITH ({Hints})");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            using (manager.Enter(FormatContext.TableRelation))
            {
                WriteLateral(writer);
                SqlValueList<Identifier> identifiers = manager.GetPseudoTable(manager.Formatting.ObjectNamePrefixWithSchema, Name.Values) ?? Name.Values;
                identifiers = manager.QuoteIdentifiers(Name.Values, identifiers);

                writer.WriteDelimited(identifiers, ".");

                FormatAlias(writer, manager);

                if (Hints != null)
                {
                    writer.WriteSql($" WITH ({Hints})");
                }
            }
        }

        /// <inheritdoc/>
        public override void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
        {
            PseudoTable[] possibleSources = pseudoTableSet.GetPseudoTables(Name);
            if (possibleSources.Length == 1)
            {
                PseudoTable tableSource = possibleSources[0];
                string refName = Alias?.Name ?? tableSource.Name;
                pseudoTableSet.AddLocalSource(refName, tableSource.RenameColumns(AliasColumns), Alias?.Name);
            }
            else
            {
                string refName = Alias?.Name ?? Name.Values.Last().Name;
                pseudoTableSet.AddLocalSource(refName, new PseudoTable.Unbound(refName, null, PseudoTableType.Table) { Alias = refName }.RenameColumns(AliasColumns), Alias?.Name);
            }
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            ItemRef? item = context.CreateObjectRef(Name);
            if (item != null)
            {
                yield return item;
            }
        }
    }

    public record TableFunction(TableFunctionCall Function) : TableFactor
    {
        public bool WithOrdinality { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            WriteLateral(writer);
            Function.ToSql(writer);
            if (WithOrdinality)
            {
                writer.Write(" WITH ORDINALITY");
            }
            WriteAlias(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            WriteLateral(writer);
            Function.FormatSql(writer, manager);
            if (WithOrdinality)
            {
                writer.Write(" WITH ORDINALITY");
            }
            FormatAlias(writer, manager);
        }

        public override void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
        {
            AddFunctionSource(pseudoTableSet, Function.Call.Name.Values.Last().Name);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return Function.GetReferencedItems(context);
        }
    }

    public record RowsFrom(SqlValueList<TableFunctionCall> Functions) : TableFactor
    {
        public bool WithOrdinality { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            WriteLateral(writer);
            writer.WriteSql($"ROWS FROM ({Functions.ToSqlDelimited()})");
            if (WithOrdinality)
            {
                writer.Write(" WITH ORDINALITY");
            }
            WriteAlias(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            WriteLateral(writer);
            writer.Write("ROWS FROM (");
            for (int index = 0; index < Functions.Count; index++)
            {
                if (index > 0)
                {
                    writer.Write(", ");
                }
                Functions[index].FormatSql(writer, manager);
            }
            writer.Write(")");
            if (WithOrdinality)
            {
                writer.Write(" WITH ORDINALITY");
            }
            FormatAlias(writer, manager);
        }

        public override void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
        {
            AddFunctionSource(pseudoTableSet, Functions[0].Call.Name.Values.Last().Name);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return Functions.SelectMany(function => function.GetReferencedItems(context));
        }
    }

    public record Derived(Select SubQuery) : TableFactor
    {
        public override void ToSql(SqlTextWriter writer)
        {
            WriteLateral(writer);
            writer.WriteSql($"({SubQuery})");
            WriteAlias(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            WriteLateral(writer);
            writer.WriteLine("(");
            manager.IncreaseIndent();
            SubQuery.FormatSql(writer, manager);
            writer.Write(")");
            FormatAlias(writer, manager, quoteAlias: false);
            manager.DecreaseIndent();
        }

        public override void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
        {
            string sourceName = Alias?.Name ?? throw new SqlSyntaxException.SubqueryNotAliased(sourceRef);
            PseudoTable newSource = SubQuery.ToPseudoTable(sourceName, null, sourceRef, PseudoTableType.DerivedTable).RenameColumns(AliasColumns);
            pseudoTableSet.AddExternalSource(sourceName, newSource);
            pseudoTableSet.AddLocalSource(sourceName, newSource, sourceName);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in SubQuery.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }

    public record NestedJoin() : TableFactor
    {
        public required TableWithJoins TableWithJoins { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            WriteLateral(writer);
            writer.WriteSql($"({TableWithJoins})");
            WriteAlias(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            WriteLateral(writer);
            writer.WriteLine("(");
            manager.IncreaseIndent();
            writer.Write(manager.Indent);
            TableWithJoins.FormatSql(writer, manager);
            manager.DecreaseIndent();
            writer.WriteLine();
            writer.Write($"{manager.Indent})");
            FormatAlias(writer, manager, quoteAlias: false);
        }

        public override void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
        {
            TableWithJoins.AddTablesToContext(pseudoTableSet, sourceRef);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in TableWithJoins.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
