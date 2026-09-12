using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

public record MyPrimaryKey(SqlValueList<KeyPart> Columns)
{
    public IndexMethod? IndexMethod { get; init; }
    public Comment? Comment { get; init; }

    public StatementTableConstraint.PrimaryKey ToStatementConstraint(DifferFormatManager manager)
    {
        var result = new StatementTableConstraint.PrimaryKey(Columns)
        {
            IndexMethod = IndexMethod,
            Comment = Comment,
        };
        if (manager.Formatting.OmitModifiersIfDefault)
        {
            if (result.IndexMethod == manager.AttributeDefaults.IndexMethod)
            {
                result = result with { IndexMethod = null };
            }
            result = result with { Columns = KeyPart.RemoveOptionalElementsFromKeyParts(result.Columns) };
        }
        return result;
    }
}
