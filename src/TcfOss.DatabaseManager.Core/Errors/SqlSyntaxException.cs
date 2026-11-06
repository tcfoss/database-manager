using System.Text;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Resources;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Errors;

public class SqlSyntaxException(string message, SourceRef? sourceRef = null)
        : SourcedException(GetMessage(message), sourceRef)
{
    public class InvalidTrigger(ObjectIdentifier trigger, string clause, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(trigger, clause), sourceRef)
    {
        public ObjectIdentifier Trigger { get; } = trigger;
        public string Clause { get; } = clause;
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_InvalidTrigger);
    }

    public class TriggerRelationNotFound(ObjectName relation, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(relation), sourceRef)
    {
        public ObjectName Relation { get; } = relation;
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_TriggerRelationNotFound);
    }

    public class TriggerFunctionNotFound(ObjectName function, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(function), sourceRef)
    {
        public ObjectName Function { get; } = function;
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_TriggerFunctionNotFound);
    }

    public class UnsupportedDialectClause(string dialect, string clause, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(dialect, clause), sourceRef)
    {
        public string Dialect { get; } = dialect;
        public string Clause { get; } = clause;

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_UnsupportedDialectClause);
    }

    public class UnsupportedDialect(string dialect, string contents, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(dialect, contents), sourceRef)
    {
        public string Dialect { get; } = dialect;
        public string Contents { get; } = contents;

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_UnsupportedDialectGeneral);
    }

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

    public class MutuallyExclusive(string attribute1, string attribute2, string objectType, string parentType, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(attribute1, attribute2, objectType, parentType), sourceRef)
    {
        private string Attribute1 { get; } = attribute1;
        private string Attribute2 { get; } = attribute2;
        private string ObjectType { get; } = objectType;
        private string ParentType { get; } = parentType;

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_MutuallyExclusive);
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

    public class TypeNotFound(ObjectName typeName, IReadOnlyList<SchemaIdentifier> searchedSchemas, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(typeName.ToSql(), string.Join(", ", searchedSchemas)), sourceRef)
    {
        public ObjectName TypeName { get; } = typeName;
        public IReadOnlyList<SchemaIdentifier> SearchedSchemas { get; } = searchedSchemas;

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_TypeNotFound);
    }

    public class CyclicTypeDependency(IReadOnlyList<ObjectIdentifier> cycle, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(string.Join(" -> ", cycle)), sourceRef)
    {
        public IReadOnlyList<ObjectIdentifier> Cycle { get; } = cycle;

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_CyclicTypeDependency);
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

    public class EmptyIndexColumnList(string indexType, SourceRef? sourceRef = null)
        : SqlSyntaxException(s_compositeFormat.Apply(indexType), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Syntax_EmptyIndexColumnList);
    }

    private static string GetMessage(string message)
    {
        return s_errorWithType.Apply(ErrorMessages.Err_Syntax, message);
    }

    private static readonly CompositeFormat s_errorWithType = CompositeFormat.Parse(ErrorMessages.ErrWithType);
};
