using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

public abstract record MyKey(SqlValueList<MyKeyPart> Columns, Identifier? IndexName)
    : IHaveOptionalIdentifierName
{
    public Comment? Comment { get; init; }
    public Identifier? Name => IndexName;

    public abstract StatementTableConstraint ToStatementConstraint(DifferFormatManager manager);

    public record Standard(SqlValueList<MyKeyPart> Columns, Identifier? IndexName) : MyKey(Columns, IndexName)
    {
        public BuiltInIndexMethod? IndexMethod { get; init; }

        public override StatementTableConstraint.Standard ToStatementConstraint(DifferFormatManager manager)
        {
            BuiltInIndexMethod? indexMethod = IndexMethod;
            if (manager.Formatting.OmitModifiersIfDefault && indexMethod == manager.AttributeDefaults.IndexMethod)
            {
                indexMethod = null;
            }
            return new StatementTableConstraint.Standard(Columns.Map(column => column.ToStatementKeyPart(manager.Formatting.OmitModifiersIfDefault)), IndexName)
            {
                IndexMethod = indexMethod,
                Comment = Comment,
                KeyLabel = KeyLabel.Key
            };
        }
    }

    public record FullText(SqlValueList<MyKeyPart> Columns, Identifier? IndexName) : MyKey(Columns, IndexName)
    {
        public override StatementTableConstraint.FullText ToStatementConstraint(DifferFormatManager manager)
        {
            return new StatementTableConstraint.FullText(Columns.Map(column => column.ToStatementKeyPart(manager.Formatting.OmitModifiersIfDefault)), IndexName)
            {
                Comment = Comment,
                KeyLabel = KeyLabel.Key
            };
        }
    }

    public record Spatial(SqlValueList<MyKeyPart> Columns, Identifier? IndexName) : MyKey(Columns, IndexName)
    {
        public override StatementTableConstraint.Spatial ToStatementConstraint(DifferFormatManager manager)
        {
            return new StatementTableConstraint.Spatial(Columns.Map(column => column.ToStatementKeyPart(manager.Formatting.OmitModifiersIfDefault)), IndexName)
            {
                Comment = Comment,
                KeyLabel = KeyLabel.Key
            };
        }
    }
}
