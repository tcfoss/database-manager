using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

public abstract record MyKey(SqlValueList<KeyPart> Columns, Identifier? IndexName)
    : IHaveOptionalIdentifierName
{
    public Comment? Comment { get; init; }
    public Identifier? Name => IndexName;

    public abstract StatementTableConstraint ToStatementConstraint(DifferFormatManager manager);

    public record Standard(SqlValueList<KeyPart> Columns, Identifier? IndexName) : MyKey(Columns, IndexName)
    {
        public IndexMethod? IndexMethod { get; init; }

        public override StatementTableConstraint.Standard ToStatementConstraint(DifferFormatManager manager)
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
            return new StatementTableConstraint.Standard(columns, IndexName)
            {
                IndexMethod = indexMethod,
                Comment = Comment,
                KeyLabel = KeyLabel.Key
            };
        }
    }

    public record FullText(SqlValueList<KeyPart> Columns, Identifier? IndexName) : MyKey(Columns, IndexName)
    {
        public override StatementTableConstraint.FullText ToStatementConstraint(DifferFormatManager manager)
        {
            SqlValueList<KeyPart> columns = Columns;
            if (manager.Formatting.OmitModifiersIfDefault)
            {
                columns = KeyPart.RemoveOptionalElementsFromKeyParts(columns);
            }
            return new StatementTableConstraint.FullText(columns, IndexName)
            {
                Comment = Comment,
                KeyLabel = KeyLabel.Key
            };
        }
    }

    public record Spatial(SqlValueList<KeyPart> Columns, Identifier? IndexName) : MyKey(Columns, IndexName)
    {
        public override StatementTableConstraint.Spatial ToStatementConstraint(DifferFormatManager manager)
        {
            SqlValueList<KeyPart> columns = Columns;
            if (manager.Formatting.OmitModifiersIfDefault)
            {
                columns = KeyPart.RemoveOptionalElementsFromKeyParts(columns);
            }
            return new StatementTableConstraint.Spatial(columns, IndexName)
            {
                Comment = Comment,
                KeyLabel = KeyLabel.Key
            };
        }
    }
}
