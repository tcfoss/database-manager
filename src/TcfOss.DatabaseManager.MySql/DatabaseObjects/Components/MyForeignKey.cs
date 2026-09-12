using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

public record MyForeignKey(SqlValueList<Identifier> Columns, ObjectIdentifier ReferencedTable, SqlValueList<Identifier> ReferencedColumns, Identifier? ConstraintName)
    : IHaveOptionalIdentifierName
{
    public ReferentialAction? OnDelete { get; init; }
    public ReferentialAction? OnUpdate { get; init; }
    public Comment? Comment { get; init; }

    public Identifier? Name => ConstraintName;

    public StatementTableConstraint.ForeignKey ToStatementConstraint(DifferFormatManager manager)
    {
        bool schemaOnForeignTable = !(!manager.Formatting.ObjectNamePrefixWithSchema && manager.Table != null && manager.Table.Name.Schema == ReferencedTable.Schema);
        ReferentialAction? onUpdate = OnUpdate;
        if (manager.Formatting.OmitModifiersIfDefault && onUpdate == manager.AttributeDefaults.ForeignKeyOnUpdate)
        {
            onUpdate = null;
        }
        ReferentialAction? onDelete = OnDelete;
        if (manager.Formatting.OmitModifiersIfDefault && onDelete == manager.AttributeDefaults.ForeignKeyOnDelete)
        {
            onDelete = null;
        }

        return new StatementTableConstraint.ForeignKey(Columns, ReferencedTable.ToObjectName(schemaOnForeignTable ? 2 : 1), ReferencedColumns, ConstraintName)
        {
            OnDelete = onDelete,
            OnUpdate = onUpdate,
            Comment = Comment
        };
    }
}
