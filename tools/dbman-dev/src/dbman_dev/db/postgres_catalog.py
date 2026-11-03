"""Selected PostgreSQL system catalog models and scoped fixture queries."""

from dbman_dev.csharp_code_generation.model_config import ModelConfig


def _class_scope(alias: str) -> str:
    return f"""EXISTS (
    SELECT 1 FROM pg_catalog.pg_namespace AS n
    WHERE n.oid = {alias}.relnamespace AND n.nspname IN ({{schemas}})
)"""


POSTGRES_CONFIGS = {
    "PG_NAMESPACE": ModelConfig(
        table_name="pg_namespace",
        key_columns=["oid"],
        data_query_template="SELECT * FROM pg_catalog.pg_namespace WHERE nspname IN ({schemas})",
    ),
    "PG_CLASS": ModelConfig(
        table_name="pg_class",
        key_columns=["oid"],
        data_query_template=f"""SELECT c.* FROM pg_catalog.pg_class AS c
    WHERE {_class_scope('c')}""",
    ),
    "PG_ATTRIBUTE": ModelConfig(
        table_name="pg_attribute",
        key_columns=["attrelid", "attnum"],
        data_query_template=f"""SELECT a.* FROM pg_catalog.pg_attribute AS a
JOIN pg_catalog.pg_class AS c ON c.oid = a.attrelid
WHERE a.attnum > 0 AND NOT a.attisdropped
    AND {_class_scope('c')}""",
    ),
    "PG_TYPE": ModelConfig(
        table_name="pg_type",
        key_columns=["oid"],
        data_query_template="""SELECT t.* FROM pg_catalog.pg_type AS t
WHERE EXISTS (SELECT 1 FROM pg_catalog.pg_namespace AS n
WHERE n.oid = t.typnamespace AND n.nspname IN ({schemas}))""",
    ),
    "PG_CONSTRAINT": ModelConfig(
        table_name="pg_constraint",
        key_columns=["oid"],
        data_query_template="""SELECT c.* FROM pg_catalog.pg_constraint AS c
WHERE EXISTS (SELECT 1 FROM pg_catalog.pg_namespace AS n
WHERE n.oid = c.connamespace AND n.nspname IN ({schemas}))""",
    ),
    "PG_INDEX": ModelConfig(
        table_name="pg_index",
        key_columns=["indexrelid"],
        data_query_template=f"""SELECT i.* FROM pg_catalog.pg_index AS i
JOIN pg_catalog.pg_class AS c ON c.oid = i.indrelid
WHERE {_class_scope('c')}""",
    ),
    "PG_PROC": ModelConfig(
        table_name="pg_proc",
        key_columns=["oid"],
        data_query_template="""SELECT p.* FROM pg_catalog.pg_proc AS p
WHERE EXISTS (SELECT 1 FROM pg_catalog.pg_namespace AS n
WHERE n.oid = p.pronamespace AND n.nspname IN ({schemas}))""",
    ),
    "PG_TRIGGER": ModelConfig(
        table_name="pg_trigger",
        key_columns=["oid"],
        data_query_template=f"""SELECT t.* FROM pg_catalog.pg_trigger AS t
JOIN pg_catalog.pg_class AS c ON c.oid = t.tgrelid
    WHERE NOT t.tgisinternal AND {_class_scope('c')}""",
    ),
    "PG_REWRITE": ModelConfig(
        table_name="pg_rewrite",
        key_columns=["oid"],
        data_query_template=f"""SELECT r.* FROM pg_catalog.pg_rewrite AS r
JOIN pg_catalog.pg_class AS c ON c.oid = r.ev_class
WHERE {_class_scope('c')}""",
    ),
    "PG_DEPEND": ModelConfig(
        table_name="pg_depend",
        key_columns=[
            "classid",
            "objid",
            "objsubid",
            "refclassid",
            "refobjid",
            "refobjsubid",
            "deptype",
        ],
        data_query_template="""SELECT d.* FROM pg_catalog.pg_depend AS d
WHERE d.refobjid IN (
    SELECT c.oid FROM pg_catalog.pg_class AS c
    JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
    WHERE n.nspname IN ({schemas})
)""",
    ),
    "PG_DESCRIPTION": ModelConfig(
        table_name="pg_description",
        key_columns=["objoid", "classoid", "objsubid"],
        data_query_template="""SELECT d.* FROM pg_catalog.pg_description AS d
WHERE d.objoid IN (
    SELECT c.oid FROM pg_catalog.pg_class AS c
    JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
    WHERE n.nspname IN ({schemas})
)""",
    ),
    "PG_COLLATION": ModelConfig(
        table_name="pg_collation",
        key_columns=["oid"],
        data_query_template="""SELECT c.* FROM pg_catalog.pg_collation AS c
WHERE EXISTS (SELECT 1 FROM pg_catalog.pg_namespace AS n
WHERE n.oid = c.collnamespace AND n.nspname IN ({schemas}))""",
    ),
    "PG_ENUM": ModelConfig(
        table_name="pg_enum",
        key_columns=["oid"],
        data_query_template="""SELECT e.* FROM pg_catalog.pg_enum AS e
JOIN pg_catalog.pg_type AS t ON t.oid = e.enumtypid
JOIN pg_catalog.pg_namespace AS n ON n.oid = t.typnamespace
WHERE n.nspname IN ({schemas})""",
    ),
    "PG_SEQUENCE": ModelConfig(
        table_name="pg_sequence",
        key_columns=["seqrelid"],
        data_query_template=f"""SELECT s.* FROM pg_catalog.pg_sequence AS s
JOIN pg_catalog.pg_class AS c ON c.oid = s.seqrelid
WHERE {_class_scope('c')}""",
    ),
    "PG_ATTRDEF": ModelConfig(
        table_name="pg_attrdef",
        key_columns=["oid"],
        data_query_template=f"""SELECT a.* FROM pg_catalog.pg_attrdef AS a
JOIN pg_catalog.pg_class AS c ON c.oid = a.adrelid
WHERE {_class_scope('c')}""",
    ),
    "PG_AM": ModelConfig(
        table_name="pg_am",
        key_columns=["oid"],
        data_query_template="SELECT * FROM pg_catalog.pg_am ORDER BY oid",
    ),
}
