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

    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);
    public abstract void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null);
    public abstract IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);


    public record Table(ObjectName Name) : TableFactor
    {
        /// <summary>
        /// Provider-specific table hints (T-SQL <c>WITH (NOLOCK, ...)</c>).
        /// Null for non-T-SQL dialects.
        /// </summary>
        public SqlValueList<TableHint>? Hints { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name}");
            if (Alias != null)
            {
                writer.WriteSql($" AS {Alias}");
            }
            if (Hints != null)
            {
                writer.WriteSql($" WITH ({Hints})");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            using (manager.Enter(FormatContext.TableRelation))
            {
                SqlValueList<Identifier> identifiers = manager.GetPseudoTable(manager.Formatting.ObjectNamePrefixWithSchema, Name.Values) ?? Name.Values;
                identifiers = manager.QuoteIdentifiers(Name.Values, identifiers);

                writer.WriteDelimited(identifiers, ".");

                Identifier? alias = Alias != null ? manager.GetQuotedIdentifier(Alias) : null;
                if (alias != null)
                {
                    writer.WriteSql($" AS {alias}");
                }

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
                pseudoTableSet.AddLocalSource(refName, tableSource, Alias?.Name);
            }
            else
            {
                string refName = Alias?.Name ?? Name.Values.Last().Name;
                pseudoTableSet.AddLocalSource(refName, new PseudoTable.Unbound(refName, null, PseudoTableType.Table) { Alias = refName }, Alias?.Name);
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

    public record Derived(Select SubQuery) : TableFactor
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"({SubQuery})");
            if (Alias != null)
            {
                writer.WriteSql($" AS {Alias}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.WriteLine("(");
            manager.IncreaseIndent();
            SubQuery.FormatSql(writer, manager);
            if (Alias != null)
            {
                writer.WriteSql($") AS {Alias}");
            }
            else
            {
                writer.Write(")");
            }
            manager.DecreaseIndent();
        }

        public override void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
        {
            string sourceName = Alias?.Name ?? throw new SqlSyntaxException.SubqueryNotAliased(sourceRef);
            var newSource = SubQuery.ToPseudoTable(sourceName, null, sourceRef, PseudoTableType.DerivedTable);
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
            writer.WriteSql($"({TableWithJoins})");

            if (Alias != null)
            {
                writer.WriteSql($" AS {Alias}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.WriteLine("(");
            manager.IncreaseIndent();
            writer.Write(manager.Indent);
            TableWithJoins.FormatSql(writer, manager);
            manager.DecreaseIndent();
            writer.WriteLine();
            writer.Write($"{manager.Indent})");
            if (Alias != null)
            {
                writer.WriteSql($" AS {Alias}");
            }
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
