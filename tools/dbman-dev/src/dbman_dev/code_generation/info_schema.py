"""Explicit system-table selections for MySQL and MariaDB."""

from copy import deepcopy

from dbman_dev.code_generation.table_config import ColumnConfig, TableConfig


def _schema_query(column: str) -> str:
    return "SELECT {columns} FROM {table}" f" WHERE t.{column} IN ({{schemas}}) {{order_by}}"


BASE_CONFIGS = {
    "TABLES": TableConfig(
        table_name="TABLES",
        schema_name="information_schema",
        base_name_override="Table",
        key_properties=["TableCatalog", "TableSchema", "TableName"],
        column_configs=[
            ColumnConfig("TABLE_CATALOG"),
            ColumnConfig("TABLE_SCHEMA"),
            ColumnConfig("TABLE_NAME"),
            ColumnConfig("TABLE_TYPE"),
            ColumnConfig("ENGINE"),
            ColumnConfig("ROW_FORMAT"),
            ColumnConfig("TABLE_COLLATION"),
            ColumnConfig("CREATE_OPTIONS"),
            ColumnConfig("TABLE_COMMENT"),
        ],
    ),
    "COLUMNS": TableConfig(
        table_name="COLUMNS",
        schema_name="information_schema",
        base_name_override="Column",
        key_properties=["TableCatalog", "TableSchema", "TableName", "ColumnName"],
        column_configs=[
            ColumnConfig("TABLE_CATALOG"),
            ColumnConfig("TABLE_SCHEMA"),
            ColumnConfig("TABLE_NAME"),
            ColumnConfig("COLUMN_NAME"),
            ColumnConfig("ORDINAL_POSITION"),
            ColumnConfig("COLUMN_DEFAULT"),
            ColumnConfig("IS_NULLABLE"),
            ColumnConfig("CHARACTER_SET_NAME"),
            ColumnConfig("COLLATION_NAME"),
            ColumnConfig("COLUMN_TYPE"),
            ColumnConfig("EXTRA", nullable_override=False),
            ColumnConfig("COLUMN_COMMENT"),
            ColumnConfig("GENERATION_EXPRESSION"),
        ],
    ),
    "SCHEMATA": TableConfig(
        table_name="SCHEMATA",
        schema_name="information_schema",
        base_name_override="Schemata",
        key_properties=["CatalogName", "SchemaName"],
        column_configs=[
            ColumnConfig("CATALOG_NAME"),
            ColumnConfig("SCHEMA_NAME"),
            ColumnConfig("DEFAULT_CHARACTER_SET_NAME"),
            ColumnConfig("DEFAULT_COLLATION_NAME"),
        ],
        data_query_template=_schema_query("SCHEMA_NAME"),
    ),
    "TABLE_CONSTRAINTS": TableConfig(
        table_name="TABLE_CONSTRAINTS",
        schema_name="information_schema",
        base_name_override="TableConstraint",
        key_properties=["ConstraintCatalog", "ConstraintSchema", "ConstraintName", "TableName"],
        column_configs=[
            ColumnConfig("CONSTRAINT_CATALOG"),
            ColumnConfig("CONSTRAINT_SCHEMA"),
            ColumnConfig("CONSTRAINT_NAME"),
            ColumnConfig("TABLE_SCHEMA"),
            ColumnConfig("TABLE_NAME"),
            ColumnConfig("CONSTRAINT_TYPE"),
        ],
        data_query_template=_schema_query("CONSTRAINT_SCHEMA"),
    ),
    "REFERENTIAL_CONSTRAINTS": TableConfig(
        table_name="REFERENTIAL_CONSTRAINTS",
        schema_name="information_schema",
        base_name_override="ReferentialConstraint",
        key_properties=["ConstraintCatalog", "ConstraintSchema", "ConstraintName"],
        column_configs=[
            ColumnConfig("CONSTRAINT_CATALOG"),
            ColumnConfig("CONSTRAINT_SCHEMA"),
            ColumnConfig("CONSTRAINT_NAME"),
            ColumnConfig("UNIQUE_CONSTRAINT_CATALOG"),
            ColumnConfig("UNIQUE_CONSTRAINT_SCHEMA"),
            ColumnConfig("UNIQUE_CONSTRAINT_NAME"),
            ColumnConfig("MATCH_OPTION"),
            ColumnConfig("UPDATE_RULE"),
            ColumnConfig("DELETE_RULE"),
            ColumnConfig("TABLE_NAME"),
            ColumnConfig("REFERENCED_TABLE_NAME"),
        ],
        data_query_template=_schema_query("CONSTRAINT_SCHEMA"),
    ),
    "STATISTICS": TableConfig(
        table_name="STATISTICS",
        schema_name="information_schema",
        base_name_override="Statistics",
        key_properties=["TableCatalog", "TableSchema", "TableName", "IndexName", "SeqInIndex"],
        column_configs=[
            ColumnConfig("TABLE_CATALOG"),
            ColumnConfig("TABLE_SCHEMA"),
            ColumnConfig("TABLE_NAME"),
            ColumnConfig("NON_UNIQUE"),
            ColumnConfig("INDEX_SCHEMA"),
            ColumnConfig("INDEX_NAME"),
            ColumnConfig("SEQ_IN_INDEX"),
            ColumnConfig("COLUMN_NAME", nullable_override=False),
            ColumnConfig("COLLATION"),
            ColumnConfig("SUB_PART"),
            ColumnConfig("INDEX_TYPE"),
            ColumnConfig("INDEX_COMMENT"),
        ],
    ),
    "KEY_COLUMN_USAGE": TableConfig(
        table_name="KEY_COLUMN_USAGE",
        schema_name="information_schema",
        base_name_override="KeyColumnUsage",
        key_properties=[
            "ConstraintCatalog",
            "ConstraintSchema",
            "ConstraintName",
            "TableName",
            "OrdinalPosition",
        ],
        column_configs=[
            ColumnConfig("CONSTRAINT_CATALOG"),
            ColumnConfig("CONSTRAINT_SCHEMA"),
            ColumnConfig("CONSTRAINT_NAME"),
            ColumnConfig("TABLE_CATALOG"),
            ColumnConfig("TABLE_SCHEMA"),
            ColumnConfig("TABLE_NAME"),
            ColumnConfig("COLUMN_NAME", nullable_override=False),
            ColumnConfig("ORDINAL_POSITION"),
            ColumnConfig("POSITION_IN_UNIQUE_CONSTRAINT"),
            ColumnConfig("REFERENCED_TABLE_SCHEMA"),
            ColumnConfig("REFERENCED_TABLE_NAME"),
            ColumnConfig("REFERENCED_COLUMN_NAME"),
        ],
        data_query_template=_schema_query("CONSTRAINT_SCHEMA"),
    ),
    "CHECK_CONSTRAINTS": TableConfig(
        table_name="CHECK_CONSTRAINTS",
        schema_name="information_schema",
        base_name_override="CheckConstraint",
        key_properties=["ConstraintCatalog", "ConstraintSchema", "ConstraintName"],
        column_configs=[
            ColumnConfig("CONSTRAINT_CATALOG"),
            ColumnConfig("CONSTRAINT_SCHEMA"),
            ColumnConfig("CONSTRAINT_NAME"),
            ColumnConfig("CHECK_CLAUSE"),
        ],
        data_query_template=_schema_query("CONSTRAINT_SCHEMA"),
    ),
    "ROUTINES": TableConfig(
        table_name="ROUTINES",
        schema_name="information_schema",
        base_name_override="Routine",
        key_properties=["RoutineCatalog", "RoutineSchema", "RoutineName"],
        column_configs=[
            ColumnConfig("SPECIFIC_NAME"),
            ColumnConfig("ROUTINE_CATALOG"),
            ColumnConfig("ROUTINE_SCHEMA"),
            ColumnConfig("ROUTINE_NAME"),
            ColumnConfig("ROUTINE_TYPE"),
            ColumnConfig("CHARACTER_SET_NAME"),
            ColumnConfig("COLLATION_NAME"),
            ColumnConfig("DTD_IDENTIFIER"),
            ColumnConfig("ROUTINE_DEFINITION"),
            ColumnConfig("IS_DETERMINISTIC"),
            ColumnConfig("SQL_DATA_ACCESS"),
            ColumnConfig("SECURITY_TYPE"),
            ColumnConfig("SQL_MODE"),
            ColumnConfig("ROUTINE_COMMENT"),
            ColumnConfig("DEFINER"),
            ColumnConfig("CHARACTER_SET_CLIENT"),
            ColumnConfig("COLLATION_CONNECTION"),
            ColumnConfig("DATABASE_COLLATION"),
        ],
        data_query_template=_schema_query("ROUTINE_SCHEMA"),
    ),
    "PARAMETERS": TableConfig(
        table_name="PARAMETERS",
        schema_name="information_schema",
        base_name_override="Parameter",
        key_properties=["SpecificCatalog", "SpecificSchema", "SpecificName", "OrdinalPosition"],
        column_configs=[
            ColumnConfig("SPECIFIC_CATALOG"),
            ColumnConfig("SPECIFIC_SCHEMA"),
            ColumnConfig("SPECIFIC_NAME"),
            ColumnConfig("ORDINAL_POSITION"),
            ColumnConfig("PARAMETER_MODE"),
            ColumnConfig("PARAMETER_NAME"),
            ColumnConfig("CHARACTER_SET_NAME"),
            ColumnConfig("COLLATION_NAME"),
            ColumnConfig("DTD_IDENTIFIER"),
            ColumnConfig("ROUTINE_TYPE"),
        ],
        data_query_template=_schema_query("SPECIFIC_SCHEMA"),
    ),
    "TRIGGERS": TableConfig(
        table_name="TRIGGERS",
        schema_name="information_schema",
        base_name_override="Trigger",
        key_properties=["TriggerCatalog", "TriggerSchema", "TriggerName"],
        column_configs=[
            ColumnConfig("TRIGGER_CATALOG"),
            ColumnConfig("TRIGGER_SCHEMA"),
            ColumnConfig("TRIGGER_NAME"),
            ColumnConfig("EVENT_MANIPULATION"),
            ColumnConfig("EVENT_OBJECT_CATALOG"),
            ColumnConfig("EVENT_OBJECT_SCHEMA"),
            ColumnConfig("EVENT_OBJECT_TABLE"),
            ColumnConfig("ACTION_ORDER"),
            ColumnConfig("ACTION_STATEMENT"),
            ColumnConfig("ACTION_TIMING"),
            ColumnConfig("SQL_MODE"),
            ColumnConfig("DEFINER"),
            ColumnConfig("CHARACTER_SET_CLIENT"),
            ColumnConfig("COLLATION_CONNECTION"),
            ColumnConfig("DATABASE_COLLATION"),
        ],
        data_query_template=_schema_query("TRIGGER_SCHEMA"),
    ),
    "VIEWS": TableConfig(
        table_name="VIEWS",
        schema_name="information_schema",
        base_name_override="View",
        key_properties=["TableCatalog", "TableSchema", "TableName"],
        column_configs=[
            ColumnConfig("TABLE_CATALOG"),
            ColumnConfig("TABLE_SCHEMA"),
            ColumnConfig("TABLE_NAME"),
            ColumnConfig("VIEW_DEFINITION", nullable_override=False),
            ColumnConfig("CHECK_OPTION"),
            ColumnConfig("DEFINER", nullable_override=False),
            ColumnConfig("SECURITY_TYPE", nullable_override=False),
            ColumnConfig("CHARACTER_SET_CLIENT"),
            ColumnConfig("COLLATION_CONNECTION"),
        ],
    ),
    "EVENTS": TableConfig(
        table_name="EVENTS",
        schema_name="information_schema",
        base_name_override="Event",
        key_properties=["EventCatalog", "EventSchema", "EventName"],
        column_configs=[
            ColumnConfig("EVENT_CATALOG"),
            ColumnConfig("EVENT_SCHEMA"),
            ColumnConfig("EVENT_NAME"),
            ColumnConfig("DEFINER"),
            ColumnConfig("TIME_ZONE"),
            ColumnConfig("EVENT_DEFINITION"),
            ColumnConfig("EVENT_TYPE"),
            ColumnConfig("EXECUTE_AT"),
            ColumnConfig("INTERVAL_VALUE"),
            ColumnConfig("INTERVAL_FIELD"),
            ColumnConfig("SQL_MODE"),
            ColumnConfig("STARTS"),
            ColumnConfig("ENDS"),
            ColumnConfig("STATUS"),
            ColumnConfig("ON_COMPLETION"),
            ColumnConfig("EVENT_COMMENT"),
            ColumnConfig("CHARACTER_SET_CLIENT"),
            ColumnConfig("COLLATION_CONNECTION"),
            ColumnConfig("DATABASE_COLLATION"),
        ],
        data_query_template=_schema_query("EVENT_SCHEMA"),
    ),
    "CHARACTER_SETS": TableConfig(
        table_name="CHARACTER_SETS",
        schema_name="information_schema",
        base_name_override="CharacterSet",
        key_properties=["CharacterSetName"],
        column_configs=[
            ColumnConfig("CHARACTER_SET_NAME"),
            ColumnConfig("DEFAULT_COLLATE_NAME"),
        ],
        filter_by_schema=False,
    ),
    "COLLATION_CHARACTER_SET_APPLICABILITY": TableConfig(
        table_name="COLLATION_CHARACTER_SET_APPLICABILITY",
        schema_name="information_schema",
        base_name_override="CollationCharacterSetApplicability",
        key_properties=["CollationName"],
        column_configs=[
            ColumnConfig("COLLATION_NAME"),
            ColumnConfig("CHARACTER_SET_NAME"),
        ],
        filter_by_schema=False,
    ),
    "ENGINES": TableConfig(
        table_name="ENGINES",
        schema_name="information_schema",
        base_name_override="Engine",
        key_properties=["Engine"],
        column_configs=[
            ColumnConfig("ENGINE"),
            ColumnConfig("SUPPORT"),
        ],
        filter_by_schema=False,
    ),
}


def _extend_columns(
    configs: dict[str, TableConfig], table: str, columns: list[ColumnConfig]
) -> None:
    configs[table].column_configs = [*(configs[table].column_configs or []), *columns]


_MYSQL_CONFIGS = deepcopy(BASE_CONFIGS)
_extend_columns(_MYSQL_CONFIGS, "COLUMNS", [ColumnConfig("SRS_ID")])
_extend_columns(_MYSQL_CONFIGS, "SCHEMATA", [ColumnConfig("DEFAULT_ENCRYPTION")])
_extend_columns(_MYSQL_CONFIGS, "TABLE_CONSTRAINTS", [ColumnConfig("ENFORCED")])
_extend_columns(
    _MYSQL_CONFIGS,
    "STATISTICS",
    [
        ColumnConfig("IS_VISIBLE"),
        ColumnConfig("EXPRESSION"),
    ],
)

_MARIADB_CONFIGS = deepcopy(BASE_CONFIGS)
_extend_columns(_MARIADB_CONFIGS, "SCHEMATA", [ColumnConfig("SCHEMA_COMMENT")])
_extend_columns(_MARIADB_CONFIGS, "STATISTICS", [ColumnConfig("IGNORED")])
_extend_columns(
    _MARIADB_CONFIGS,
    "CHECK_CONSTRAINTS",
    [
        ColumnConfig("TABLE_NAME"),
        ColumnConfig("LEVEL"),
    ],
)
_extend_columns(_MARIADB_CONFIGS, "VIEWS", [ColumnConfig("ALGORITHM")])
_extend_columns(
    _MARIADB_CONFIGS,
    "COLLATION_CHARACTER_SET_APPLICABILITY",
    [
        ColumnConfig("FULL_COLLATION_NAME"),
        ColumnConfig("IS_DEFAULT"),
    ],
)
_MARIADB_CONFIGS["COLLATION_CHARACTER_SET_APPLICABILITY"].key_properties = ["FullCollationName"]

MYSQL_CONFIGS = list(_MYSQL_CONFIGS.values())
MARIADB_CONFIGS = list(_MARIADB_CONFIGS.values())
