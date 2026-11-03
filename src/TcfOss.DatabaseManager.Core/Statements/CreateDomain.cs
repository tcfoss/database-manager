using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record CreateDomain(ObjectName Name, DataTypeReference DataType) : Statement, ICreateStatement
{
    public bool PrintAs { get; init; } = true;
    public ObjectName? Collation { get; init; }
    public Expression? Default { get; init; }
    public SqlValueList<StatementDomainConstraint> Constraints { get; init; } = [];

    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"CREATE DOMAIN {Name}");
        writer.Write(PrintAs ? " AS " : " ");
        writer.WriteSql($"{DataType}");
        if (Collation != null)
        {
            writer.WriteSql($" COLLATE {Collation}");
        }
        if (Default != null)
        {
            writer.WriteSql($" DEFAULT {Default}");
        }
        foreach (StatementDomainConstraint constraint in Constraints)
        {
            writer.WriteSql($" {constraint}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSql($"{manager.Indent}CREATE DOMAIN ");
        Name.FormatSql(writer, manager);
        writer.Write(PrintAs ? " AS " : " ");
        DataType.FormatSql(writer, manager);
        if (Collation != null)
        {
            writer.Write(" COLLATE ");
            Collation.FormatSql(writer, manager);
        }
        if (Default != null)
        {
            writer.Write(" DEFAULT ");
            Default.FormatSql(writer, manager);
        }
        foreach (StatementDomainConstraint constraint in Constraints)
        {
            writer.Write(" ");
            constraint.FormatSql(writer, manager);
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (Default != null)
        {
            foreach (ItemRef item in Default.GetReferencedItems(context))
            {
                yield return item;
            }
        }
        foreach (StatementDomainConstraint.Check check in Constraints.OfType<StatementDomainConstraint.Check>())
        {
            foreach (ItemRef item in check.Expression.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
