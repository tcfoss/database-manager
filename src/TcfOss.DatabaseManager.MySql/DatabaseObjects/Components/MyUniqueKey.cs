using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

public record MyUniqueKey(SqlValueList<KeyPart> Columns, Identifier? ConstraintName)
    : IHaveOptionalIdentifierName
{
    public IndexMethod? IndexMethod { get; init; }
    public Comment? Comment { get; init; }

    public Identifier? Name => ConstraintName;

    public StatementTableConstraint.UniqueConstraint ToStatementConstraint(DifferFormatManager manager)
    {
        IndexMethod? indexMethod = IndexMethod;
        if (manager.Formatting.OmitModifiersIfDefault && indexMethod == manager.AttributeDefaults.IndexMethod)
        {
            indexMethod = null;
        }
        SqlValueList<KeyPart> columns = Columns;
        if (manager.Formatting.OmitModifiersIfDefault)
        {
            columns = KeyPart.RemoveOptionalElementsFromKeyParts(columns);
        }
        return new StatementTableConstraint.UniqueConstraint(columns, ConstraintName)
        {
            IndexMethod = indexMethod,
            Comment = Comment,
            KeyLabel = KeyLabel.Key
        };
    }
}
