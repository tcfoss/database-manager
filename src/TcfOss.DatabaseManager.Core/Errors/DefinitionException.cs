using System.Text;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Resources;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Errors;

public class DefinitionException(string message, SourceRef? sourceRef = null) : SourcedException(GetMessage(message), sourceRef)
{
    public class UniqueColumn(ColumnIdentifier identifier, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(identifier), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_Table_UniqueOnColumn);
    }

    public class CheckColumn(ColumnIdentifier identifier, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(identifier), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_Table_CheckOnColumn);
    }

    public class TableConstraintNameRequired(string constraintType, string tableName, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(constraintType, tableName), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_Table_UnnamedConstraint);
    }

    public class CreateTableAsSelect(string tableName, SourceRef? sourceRef)
        : DefinitionException(s_compositeFormat.Apply(tableName), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_Table_SelectAs);
    }

    public class MissingTableColumns(string tableName, SourceRef? sourceRef)
        : DefinitionException(s_compositeFormat.Apply(tableName), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_Table_NoColumns);
    }

    public class ViewSelectUnexpectedItem(string? viewName, object item, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(viewName, item), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_View_UnexpectedSelectItem);
    }
    public class ViewSelectWildcard(string? viewName, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(viewName), sourceRef)
    {
        public ViewSelectWildcard(ObjectIdentifier? viewName, SourceRef? sourceRef = null)
            : this(viewName?.ToString(), sourceRef)
        { }

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_View_SelectWildcard);
    }

    public class ViewSelectWithoutAlias(string? viewName, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(viewName), sourceRef)
    {
        public ViewSelectWithoutAlias(ObjectIdentifier? viewName, SourceRef? sourceRef = null)
            : this(viewName?.ToString(), sourceRef) { }

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_View_SelectWithoutAlias);
    }

    public class ViewNonUniqueSelectionName(string? viewName, string? name, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(viewName, name), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_View_NonUniqueSelection);
    }

    public class NotLiteralEventDate(string eventName, string givenDate, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(eventName, givenDate), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_Event_NonLiteralDate);
    }

    public class MissingDefiner(string objectType, string objectName, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(objectType, objectName), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_ObjectNoDefiner);
    }

    public class ImplicitDefiner(string objectType, string objectName, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(objectType, objectName), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_ObjectImplicitDefiner);
    }

    public class MissingSecurityContext(string objectType, string objectName, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(objectType, objectName), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_ObjectNoSecurityContext);
    }

    public class TriggerOrderViaPrecedes(string triggerName, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(triggerName), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_Trigger_PrecedesUnsupported);
    }

    public class UndefinedTriggerOrder(string triggerName1, string triggerName2, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(triggerName1, triggerName2), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_Trigger_UndefinedOrder);
    }


    public class ForeignKeyNoBackingIndex(string foreignKeyId, ObjectIdentifier tableId, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(foreignKeyId, tableId), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_ForeignKeyNoBackingIndex);
    }

    public class ForeignKeyNoReferencedBackingIndex(string foreignKeyId, ObjectIdentifier tableId, ObjectIdentifier referenceTableId, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(foreignKeyId, tableId, referenceTableId), sourceRef)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_ForeignKeyNoReferencedBackingIndex);
    }

    public class StringAttributeMissing(Identifier columnId, ObjectIdentifier tableId, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(columnId, tableId), sourceRef)
    {
        public StringAttributeMissing(ColumnIdentifier columnId, SourceRef? sourceRef = null)
            : this(columnId.ToSimpleIdentifier(), columnId.Table, sourceRef) { }

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_DataTypeStringAttributeMissing);
    }

    public class StringAttributeCharacterSetMissing(Identifier columnId, ObjectIdentifier tableId, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(columnId, tableId), sourceRef)
    {
        public StringAttributeCharacterSetMissing(ColumnIdentifier columnId, SourceRef? sourceRef = null)
            : this(columnId.ToSimpleIdentifier(), columnId.Table, sourceRef) { }

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_DataTypeStringNoCharacterSet);
    }

    public class StringAttributeCollationMissing(Identifier columnId, ObjectIdentifier tableId, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(columnId, tableId), sourceRef)
    {
        public StringAttributeCollationMissing(ColumnIdentifier columnId, SourceRef? sourceRef = null)
            : this(columnId.ToSimpleIdentifier(), columnId.Table, sourceRef) { }

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_DataTypeStringNoCollation);
    }

    public class StringAttributeInvalidCharacterSet(Identifier columnId, ObjectIdentifier tableId, string characterSet, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(characterSet, columnId, tableId), sourceRef)
    {
        public StringAttributeInvalidCharacterSet(ColumnIdentifier columnId, string characterSet, SourceRef? sourceRef = null)
            : this(columnId.ToSimpleIdentifier(), columnId.Table, characterSet, sourceRef) { }

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_DataTypeStringInvalidCharacterSet);
    }

    public class StringAttributeInvalidCollation(Identifier columnId, ObjectIdentifier tableId, string characterSet, string collation, SourceRef? sourceRef = null)
        : DefinitionException(s_compositeFormat.Apply(collation, columnId, tableId, characterSet), sourceRef)
    {
        public StringAttributeInvalidCollation(ColumnIdentifier columnId, string characterSet, string collation, SourceRef? sourceRef = null)
            : this(columnId.ToSimpleIdentifier(), columnId.Table, characterSet, collation, sourceRef) { }

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Def_DataTypeStringInvalidCollation);
    }

    private static string GetMessage(string message)
    {
        return s_errorWithType.Apply(ErrorMessages.Err_Def, message);
    }

    private static readonly CompositeFormat s_errorWithType = CompositeFormat.Parse(ErrorMessages.ErrWithType);
}
