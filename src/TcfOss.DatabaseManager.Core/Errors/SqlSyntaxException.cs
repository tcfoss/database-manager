using System.Text;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Resources;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Errors;

public class SqlSyntaxException(string message, SourceRef? sourceRef = null)
        : SourcedException(GetMessage(message), sourceRef)
{
    public class DuplicateObject(string objectName, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(objectName), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_DuplicateObject);
    }

    public class DuplicateColumn(string columnName, ObjectIdentifier tableName, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(columnName, tableName), sourceRef)
    {
        public DuplicateColumn(ColumnIdentifier columnIdentifier, SourceRef? sourceRef = null)
            : this(columnIdentifier.Name, columnIdentifier.Table, sourceRef) { }

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_DuplicateColumn);
    }

    public class SpecifiedMoreThanOnce(string objectType, string parentType, Identifier parentName, SourceRef? sourceRef = null)
            : SqlSyntaxException(s_compositeFormat.Apply(objectType, parentType, parentName), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_AttributeSpecifiedMultipleTimes);
    }

    public class ConstraintNameNotAllowedException(string constraintName, string objectType, string parentType, Identifier parentName, string? dialect = null, SourceRef? sourceRef = null)
            : SqlSyntaxException(dialect != null ? s_compositeFormatDialect.Apply(constraintName, objectType, parentType, parentName, dialect) : s_compositeFormat.Apply(constraintName, objectType, parentType, parentName), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_ConstraintNameNotAllowed);
        private static readonly CompositeFormat s_compositeFormatDialect = CompositeFormat.Parse(ErrorMessages.Err_Syntax_ConstraintNameNotAllowedDialect);
    }

    public class SelectSourceNotFound(string identifier, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(identifier), sourceRef)
    {
        public SelectSourceNotFound(string?[] parts, SourceRef? sourceRef = null)
            : this(parts.ToIdentifier(), sourceRef) { }

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_UnknownTable);
    }

    public class SelectIdentifierNotFound(string identifier, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(identifier), sourceRef)
    {
        public SelectIdentifierNotFound(string?[] parts, SourceRef? sourceRef = null)
            : this(parts.ToIdentifier(), sourceRef) { }

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_UnknownIdentifier);
    }

    public class SelectIdentifierNotUnique(string identifier, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(identifier), sourceRef)
    {
        public SelectIdentifierNotUnique(string?[] parts, SourceRef? sourceRef = null)
            : this(parts.ToIdentifier(), sourceRef) { }

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_SelectIdentifierNotUnique);
    }

    public class ViewNonUniqueReferenceName(string identifier, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(identifier), sourceRef)
    {
        public ViewNonUniqueReferenceName(string?[] parts, SourceRef? sourceRef = null)
            : this(parts.ToIdentifier(), sourceRef) { }

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_TableIdentifierNotUnique);
    }

    public class IndexDirectionNotSupported(string? keyName, string indexType, string tableName, SourceRef? sourceRef = null)
        : SqlSyntaxException(keyName != null ? s_compositeFormat.Apply(keyName, tableName, indexType) : s_compositeFormatUnnamed.Apply(tableName, indexType), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_IndexDirectionNotSupportedNamed);
        private static readonly CompositeFormat s_compositeFormatUnnamed = CompositeFormat.Parse(ErrorMessages.Err_Syntax_IndexDirectionNotSupportedUnnamed);
    }

    public class AutoIncrementPrimaryKeyOnly(Identifier columnId, ObjectIdentifier tableId, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(columnId, tableId), sourceRef)
    {
        public AutoIncrementPrimaryKeyOnly(ColumnIdentifier columnId, SourceRef? sourceRef = null)
            : this(columnId.ToSimpleIdentifier(), columnId.Table, sourceRef) { }
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_AutoIncrementPkOnly);
    }

    public class AutoIncrementIntegerOnly(Identifier columnId, ObjectIdentifier tableId, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(columnId, tableId), sourceRef)
    {
        public AutoIncrementIntegerOnly(ColumnIdentifier columnId, SourceRef? sourceRef = null)
            : this(columnId.ToSimpleIdentifier(), columnId.Table, sourceRef) { }
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_AutoIncrementNonInteger);
    }

    public class IndexColumnNotFound(string indexId, ObjectIdentifier tableId, Identifier columnId, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(indexId, tableId, columnId), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_IndexColumnNotFound);
    }

    public class ForeignKeyReferencedTableNotFound(string foreignKeyId, ObjectIdentifier tableId, ObjectIdentifier referenceTableId, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(foreignKeyId, tableId, referenceTableId), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_ForeignKeyReferencedTableNotFound);
    }

    public class ForeignKeyReferencedColumnNotFound(string foreignKeyId, ObjectIdentifier tableId, ObjectIdentifier referenceTableId, string referenceColumnId, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(foreignKeyId, tableId, referenceColumnId, referenceTableId), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_ForeignKeyReferencedColumnNotFound);
    }

    public class ForeignKeyColumnCountMismatch(string foreignKeyId, ObjectIdentifier tableId, int columnCount, int referenceColumnCount, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(foreignKeyId, tableId, columnCount, referenceColumnCount), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_ForeignKeyColumnCountMismatch);
    }

    public class ForeignKeyColumnTypeMismatch(string foreignKeyId, ObjectIdentifier tableId, string columnId, DataType dataType, ObjectIdentifier referenceTableId, string referenceColumnId, DataType referencedType, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(foreignKeyId, tableId, columnId, dataType, referencedType, referenceColumnId, referenceTableId), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_ForeignKeyTypeMismatch);
    }

    public class SubqueryNotAliased(SourceRef? sourceRef)
        : SqlSyntaxException(ErrorMessages.Err_Syntax_UnaliasedSubquery, sourceRef);

    public class CreateIndexUnknownTable(ObjectIdentifier tableName, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(tableName), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_CreateIndexUnknownTable);
    }

    public class CreateIndexUnexpectedType(string indexType, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(indexType), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_CreateIndexUnexpectedType);
    }

    public class TriggerSupportsOnlyOneEvent(string dialect, string triggerName, int eventCount, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(dialect, triggerName, eventCount), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_TriggerSupportsOnlyOneEvent);
    }

    private static string GetMessage(string message)
    {
        return s_errorWithType.Apply(ErrorMessages.Err_Syntax, message);
    }

    private static readonly CompositeFormat s_errorWithType = CompositeFormat.Parse(ErrorMessages.ErrWithType);
};
