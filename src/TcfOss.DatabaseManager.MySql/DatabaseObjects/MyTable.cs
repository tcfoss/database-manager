using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;
using TcfOss.DatabaseManager.MySql.Statements.Components;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects;

public record MyTable(ObjectIdentifier Name, SqlValueList<MyColumn> Columns)
    : ITable
{
    public ObjectType ObjectType => ObjectType.Table;

    public MyPrimaryKey? PrimaryKey { get; init; }
    public DatabaseComponentDict<MyKey> Keys { get; init; } = [];
    public DatabaseComponentDict<MyUniqueKey> UniqueKeys { get; init; } = [];
    public DatabaseComponentDict<MyForeignKey> ForeignKeys { get; init; } = [];
    public DatabaseComponentDict<MyCheck> Checks { get; init; } = [];

    public required string Engine { get; init; }
    public required string CharacterSet { get; init; }
    public required string Collation { get; init; }
    public ulong? AutoIncrement { get; init; }
    public Comment? TableComment { get; init; }

    SqlValueList<IColumn> ITable.Columns => [.. Columns];
    public bool Equals(ITable? other) => Equals(other as MyTable);

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context) => throw new NotImplementedException();

    Statement IDatabaseObject.ToCreateStatement(bool includeSchema, DifferFormatManager manager) => ToCreateStatement(includeSchema, manager);

    public CreateTable ToCreateStatement(bool includeSchema, DifferFormatManager manager)
    {
        SqlValueList<StatementColumn> columns = [.. Columns.Select(c => c.ToStatementColumn(manager))];

        var name = Name.ToObjectName(includeSchema ? 2 : 1);

        SqlValueList<StatementTableConstraint> constraints = [];

        if (PrimaryKey != null)
        {
            constraints.Add(PrimaryKey.ToStatementConstraint(manager));
        }
        constraints.AddRange(UniqueKeys.Values.Select(x => x.ToStatementConstraint(manager)));
        constraints.AddRange(Keys.Values.Select(x => x.ToStatementConstraint(manager)));
        if (!manager.OmitForeignKeysOnCreateTable)
        {
            constraints.AddRange(ForeignKeys.Values.Select(x => x.ToStatementConstraint(
                manager with
                {
                    Table = this,
                })));
        }
        constraints.AddRange(Checks.Values.Select(x => x.ToStatementConstraint()));

        SqlValueList<StatementTableOption> tableOptions = [
            new MyStatementTableOption.Engine(Engine),
            new MyStatementTableOption.CharacterSet(CharacterSet),
            new MyStatementTableOption.Collation(Collation)
        ];

        if (TableComment != null)
        {
            tableOptions.Add(new MyStatementTableOption.Comment(TableComment.Text));
        }

        return new CreateTable(name, columns, constraints)
        {
            TableOptions = tableOptions,
        };
    }

    public IEnumerable<NamedKeyPartList> GetPotentialKeyPartLists()
    {
        if (PrimaryKey != null)
        {
            yield return new NamedKeyPartList(null, PrimaryKey.Columns);
        }
        foreach (MyUniqueKey uniqueKey in UniqueKeys.Values)
        {
            yield return new NamedKeyPartList(uniqueKey.ConstraintName, uniqueKey.Columns);
        }
        foreach (MyKey key in Keys.Values)
        {
            yield return new NamedKeyPartList(key.IndexName, key.Columns);
        }
    }

    public PseudoTable ToPseudoTable()
    {
        return new PseudoTable(Name.Name, Name, [.. Columns.Select(c => c.Name.Name)], PseudoTableType.Table);
    }
}
