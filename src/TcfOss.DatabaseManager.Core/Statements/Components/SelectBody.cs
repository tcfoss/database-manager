using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record SelectBody() : IWriteSql, IHaveMeta, IAddTablesToContext
{
    public MetaData Meta { get; set; } = new();

    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public abstract PseudoTable ToPseudoTable(string name, ObjectIdentifier? identifier, SourceRef? sourceRef, PseudoTableType type);

    public abstract PseudoTable ToPseudoTableRelaxed(string name, ObjectIdentifier? identifier, SourceRef? sourceRef, PseudoTableType type);

    public virtual void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
    { }

    public abstract IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);


    public record SimpleSelectQuery(SimpleSelect Query) : SelectBody
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Query.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Query.FormatSql(writer, manager);
        }

        public override PseudoTable ToPseudoTable(string name, ObjectIdentifier? identifier, SourceRef? sourceRef, PseudoTableType type)
        {
            return Query.ToPseudoTable(name, identifier, sourceRef, type);
        }

        public override PseudoTable ToPseudoTableRelaxed(string name, ObjectIdentifier? identifier, SourceRef? sourceRef, PseudoTableType type)
        {
            return Query.ToPseudoTableRelaxed(name, identifier, sourceRef, type);
        }

        public override void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
        {
            Query.AddTablesToContext(pseudoTableSet, sourceRef);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            context.PseudoTables?.EnterSelectScope();
            try
            {
                foreach (ItemRef item in Query.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
            finally
            {
                context.PseudoTables?.LeaveSelectScope();
            }
        }
    }


    public record SelectQuery(Select Query) : SelectBody
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Query.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Query.FormatSql(writer, manager);
        }

        public override PseudoTable ToPseudoTable(string name, ObjectIdentifier? identifier, SourceRef? sourceRef, PseudoTableType type)
        {
            return Query.ToPseudoTable(name, identifier, sourceRef, type);
        }

        public override PseudoTable ToPseudoTableRelaxed(string name, ObjectIdentifier? identifier, SourceRef? sourceRef, PseudoTableType type)
        {
            return Query.ToPseudoTableRelaxed(name, identifier, sourceRef, type);
        }

        public override void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
        {
            Query.AddTablesToContext(pseudoTableSet, sourceRef);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return Query.GetReferencedItems(context);
        }
    }


    public record SetOperation(SelectBody Left, SetOperator Operator, SelectBody Right, SetQuantifier? Quantifier = null) : SelectBody
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Left} {Operator}");

            if (Quantifier != null)
            {
                writer.WriteSql($" {Quantifier}");
            }

            writer.WriteSql($" {Right}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Left.FormatSql(writer, manager);
            writer.WriteLine();

            writer.WriteSqlI($"{Operator}");
            if (Quantifier != null)
            {
                writer.WriteSql($" {Quantifier}");
            }
            writer.WriteLine();

            Right.FormatSql(writer, manager);
        }

        public override PseudoTable ToPseudoTable(string name, ObjectIdentifier? identifier, SourceRef? sourceRef, PseudoTableType type)
        {
            try
            {
                return Left.ToPseudoTable(name, identifier, sourceRef, type);
            }
            catch (SourcedException)
            {
                return Right.ToPseudoTable(name, identifier, sourceRef, type);
            }
        }

        public override PseudoTable ToPseudoTableRelaxed(string name, ObjectIdentifier? identifier, SourceRef? sourceRef, PseudoTableType type)
        {
            try
            {
                return Left.ToPseudoTableRelaxed(name, identifier, sourceRef, type);
            }
            catch (SourcedException)
            {
                return Right.ToPseudoTableRelaxed(name, identifier, sourceRef, type);
            }
        }

        public override void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
        {
            Left.AddTablesToContext(pseudoTableSet, sourceRef);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Left.GetReferencedItems(context))
            {
                yield return item;
            }

            foreach (ItemRef item in Right.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }

    public record ValuesQuery(Values Values) : SelectBody
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Values.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Values.FormatSql(writer, manager);
        }

        public override PseudoTable ToPseudoTable(string name, ObjectIdentifier? identifier, SourceRef? sourceRef, PseudoTableType type)
        {
            throw new InvalidOperationException("Cannot convert VALUES query to PseudoTable.");
        }

        public override PseudoTable ToPseudoTableRelaxed(string name, ObjectIdentifier? identifier, SourceRef? sourceRef, PseudoTableType type)
        {
            throw new InvalidOperationException("Cannot convert VALUES query to PseudoTable.");
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (SqlValueList<Expression> row in Values.Rows)
            {
                foreach (Expression expr in row)
                {
                    foreach (ItemRef item in expr.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}
