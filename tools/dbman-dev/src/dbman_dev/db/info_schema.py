"""Tables from the INFORMATION_SCHEMA database."""

from dbman_dev.csharp_code_generation.model_config import ModelConfig

BASE_CONFIGS = {
    "TABLES": ModelConfig(
        table_name="TABLES",
        key_columns=["TABLE_CATALOG", "TABLE_SCHEMA", "TABLE_NAME"],
        model_name_override="Table",
    ),
    "COLUMNS": ModelConfig(
        table_name="COLUMNS",
        key_columns=["TABLE_CATALOG", "TABLE_SCHEMA", "TABLE_NAME", "COLUMN_NAME"],
        model_name_override="Column",
        ignored_columns=["IS_SYSTEM_TIME_PERIOD_START", "IS_SYSTEM_TIME_PERIOD_END"],
    ),
    "SCHEMATA": ModelConfig(
        table_name="SCHEMATA",
        key_columns=["CATALOG_NAME", "SCHEMA_NAME"],
        model_name_override="Schemata",
    ),
    "TABLE_CONSTRAINTS": ModelConfig(
        table_name="TABLE_CONSTRAINTS",
        key_columns=["CONSTRAINT_CATALOG", "CONSTRAINT_SCHEMA", "CONSTRAINT_NAME", "TABLE_NAME"],
        model_name_override="TableConstraint",
    ),
    "REFERENTIAL_CONSTRAINTS": ModelConfig(
        table_name="REFERENTIAL_CONSTRAINTS",
        key_columns=["CONSTRAINT_CATALOG", "CONSTRAINT_SCHEMA", "CONSTRAINT_NAME"],
        model_name_override="ReferentialConstraint",
    ),
    "STATISTICS": ModelConfig(
        table_name="STATISTICS",
        key_columns=["TABLE_CATALOG", "TABLE_SCHEMA", "TABLE_NAME", "INDEX_NAME", "SEQ_IN_INDEX"],
        model_name_override="Statistics",
        plural_name_override="Statistics",
        not_null_overrides=["COLUMN_NAME"],
    ),
    "KEY_COLUMN_USAGE": ModelConfig(
        table_name="KEY_COLUMN_USAGE",
        key_columns=[
            "CONSTRAINT_CATALOG",
            "CONSTRAINT_SCHEMA",
            "CONSTRAINT_NAME",
            "TABLE_NAME",
            "ORDINAL_POSITION",
        ],
        model_name_override="KeyColumnUsage",
        not_null_overrides=["COLUMN_NAME"],
    ),
    "CHECK_CONSTRAINTS": ModelConfig(
        table_name="CHECK_CONSTRAINTS",
        key_columns=["CONSTRAINT_CATALOG", "CONSTRAINT_SCHEMA", "CONSTRAINT_NAME"],
        model_name_override="CheckConstraint",
    ),
    "ROUTINES": ModelConfig(
        table_name="ROUTINES",
        key_columns=["ROUTINE_CATALOG", "ROUTINE_SCHEMA", "ROUTINE_NAME"],
        model_name_override="Routine",
    ),
    "PARAMETERS": ModelConfig(
        table_name="PARAMETERS",
        key_columns=["SPECIFIC_CATALOG", "SPECIFIC_SCHEMA", "SPECIFIC_NAME", "ORDINAL_POSITION"],
        model_name_override="Parameter",
    ),
    "TRIGGERS": ModelConfig(
        table_name="TRIGGERS",
        key_columns=["TRIGGER_CATALOG", "TRIGGER_SCHEMA", "TRIGGER_NAME"],
        model_name_override="Trigger",
    ),
    "VIEWS": ModelConfig(
        table_name="VIEWS",
        key_columns=["TABLE_CATALOG", "TABLE_SCHEMA", "TABLE_NAME"],
        model_name_override="View",
        not_null_overrides=["VIEW_DEFINITION", "SECURITY_TYPE", "DEFINER"],
    ),
    "EVENTS": ModelConfig(
        table_name="EVENTS",
        key_columns=["EVENT_CATALOG", "EVENT_SCHEMA", "EVENT_NAME"],
        model_name_override="Event",
    ),
    "CHARACTER_SETS": ModelConfig(
        table_name="CHARACTER_SETS",
        key_columns=["CHARACTER_SET_NAME"],
        model_name_override="CharacterSet",
    ),
    "COLLATION_CHARACTER_SET_APPLICABILITY": ModelConfig(
        table_name="COLLATION_CHARACTER_SET_APPLICABILITY",
        key_columns=["COLLATION_NAME"],
        model_name_override="CollationCharacterSetApplicability",
    ),
    "ENGINES": ModelConfig(
        table_name="ENGINES", key_columns=["ENGINE"], model_name_override="Engine"
    ),
}

MYSQL_CONFIGS = BASE_CONFIGS.copy()

MARIADB_CONFIGS = BASE_CONFIGS.copy()
MARIADB_CONFIGS.update(
    {
        "COLLATION_CHARACTER_SET_APPLICABILITY": ModelConfig(
            table_name="COLLATION_CHARACTER_SET_APPLICABILITY",
            key_columns=["FULL_COLLATION_NAME"],
            model_name_override="CollationCharacterSetApplicability",
        )
    }
)
