using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("TRIGGER", "Trigger")]
[StringEnum("VOID", "Void")]
[StringEnum("RECORD", "Record")]
[StringEnum("ANY", "Any")]
[StringEnum("ANYELEMENT", "AnyElement")]
[StringEnum("ANYARRAY", "AnyArray")]
[StringEnum("ANYNONARRAY", "AnyNonArray")]
[StringEnum("ANYENUM", "AnyEnum")]
[StringEnum("ANYRANGE", "AnyRange")]
[StringEnum("ANYMULTIRANGE", "AnyMultiRange")]
[StringEnum("ANYCOMPATIBLE", "AnyCompatible")]
[StringEnum("ANYCOMPATIBLEARRAY", "AnyCompatibleArray")]
[StringEnum("ANYCOMPATIBLENONARRAY", "AnyCompatibleNonArray")]
[StringEnum("ANYCOMPATIBLERANGE", "AnyCompatibleRange")]
[StringEnum("ANYCOMPATIBLEMULTIRANGE", "AnyCompatibleMultiRange")]
[StringEnum("CSTRING", "CString")]
[StringEnum("INTERNAL", "Internal")]
[StringEnum("LANGUAGE_HANDLER", "LanguageHandler")]
[StringEnum("FDW_HANDLER", "FdwHandler")]
[StringEnum("TABLE_AM_HANDLER", "TableAmHandler")]
[StringEnum("INDEX_AM_HANDLER", "IndexAmHandler")]
[StringEnum("TSM_HANDLER", "TsmHandler")]
[StringEnum("EVENT_TRIGGER", "EventTrigger")]
[StringEnum("PG_DDL_COMMAND", "PgDdlCommand")]
public sealed partial class PgPseudoType;
