using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

public record MyPrimaryKey(SqlValueList<MyKeyPart> Columns)
{
    public BuiltInIndexMethod? IndexMethod { get; init; }
    public Comment? Comment { get; init; }

    public StatementTableConstraint.PrimaryKey ToStatementConstraint(DifferFormatManager manager)
    {
        BuiltInIndexMethod? method = manager.Formatting.OmitModifiersIfDefault && IndexMethod == manager.AttributeDefaults.IndexMethod
            ? null
            : IndexMethod;

        var result = new StatementTableConstraint.PrimaryKey(Columns.Map(column => column.ToStatementKeyPart(manager.Formatting.OmitModifiersIfDefault)))
        {
            IndexMethod = method,
            Comment = Comment,
        };
        return result;
    }
}
