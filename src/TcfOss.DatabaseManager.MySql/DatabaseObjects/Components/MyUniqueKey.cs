using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

public record MyUniqueKey(SqlValueList<MyKeyPart> Columns, Identifier? ConstraintName)
    : IHaveOptionalIdentifierName
{
    public BuiltInIndexMethod? IndexMethod { get; init; }
    public Comment? Comment { get; init; }

    public Identifier? Name => ConstraintName;

    public StatementTableConstraint.UniqueConstraint ToStatementConstraint(DifferFormatManager manager)
    {
        BuiltInIndexMethod? indexMethod = IndexMethod;
        if (manager.Formatting.OmitModifiersIfDefault && indexMethod == manager.AttributeDefaults.IndexMethod)
        {
            indexMethod = null;
        }
        return new StatementTableConstraint.UniqueConstraint(Columns.Map(column => column.ToStatementKeyPart(manager.Formatting.OmitModifiersIfDefault)), ConstraintName)
        {
            IndexMethod = indexMethod,
            Comment = Comment,
            KeyLabel = KeyLabel.Key
        };
    }
}
