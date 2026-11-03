using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects;

public record PgTable(ObjectIdentifier Name, SqlValueList<PgColumn> Columns) : IDatabaseObject<CreateTable>
{
    public PgPrimaryKey? PrimaryKey { get; init; }
    public DatabaseComponentDict<PgTableConstraint.Unique> UniqueConstraints { get; init; } = [];
    public DatabaseComponentDict<PgTableConstraint.ForeignKey> ForeignKeys { get; init; } = [];
    public DatabaseComponentDict<PgTableConstraint.Check> Checks { get; init; } = [];
    public DatabaseComponentDict<PgIndex> Indexes { get; init; } = [];

    public IEnumerable<CreateIndex> ToCreateIndexStatements(bool includeSchema)
    {
        return Indexes.Values.Select(index => index.ToCreateStatement(Name, includeSchema));
    }

    public ObjectType ObjectType => ObjectType.Table;

    public CreateTable ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null)
    {
        var constraints = new SqlValueList<StatementTableConstraint>();
        if (PrimaryKey != null)
        {
            constraints.Add(PrimaryKey.ToStatementConstraint());
        }
        constraints.AddRange(Checks.Values.Select(constraint => constraint.ToStatementConstraint()));
        constraints.AddRange(ForeignKeys.Values.Select(foreignKey => foreignKey.ToStatementConstraint()));
        constraints.AddRange(UniqueConstraints.Values.Select(unique => unique.ToStatementConstraint()));
        return new CreateTable(Name.ToObjectName(includeSchema ? 2 : 1), [.. Columns.Select(column => column.ToStatementColumn(manager))], constraints);
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in ToCreateStatement(true).GetReferencedItems(context))
        {
            yield return item;
        }
        foreach (CreateIndex index in ToCreateIndexStatements(true))
        {
            foreach (ItemRef item in index.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }

    public PseudoTable ToPseudoTable()
    {
        return new PseudoTable(Name.Name, Name, [.. Columns.Select(column => column.Name.Name)], PseudoTableType.Table);
    }
}
