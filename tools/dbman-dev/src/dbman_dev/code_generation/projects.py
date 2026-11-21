"""Known projects."""

import attrs
from pathlib import Path
from dbman_dev.code_generation.dialect import SqlDialect

from dbman_dev.paths import REPO_ROOT
import dbman_dev.code_generation.data_types as dt
import dbman_dev.code_generation.project_config as pc
import dbman_dev.code_generation.table_config as tc
import dbman_dev.code_generation.info_schema as cat_my
import dbman_dev.code_generation.postgres_catalog as cat_pg
import dbman_dev.code_generation.containers as dbc

_TEST_SCHEMAS_ROOT = REPO_ROOT / "test" / "Resources" / "TestSchemas"


@attrs.frozen
class DbInitialization:
    source_path: Path
    initialization_scripts: list[str]
    provided_schemas: list[str]


_mysql_projects: list[DbInitialization] = [
    DbInitialization(
        source_path=_TEST_SCHEMAS_ROOT / "MySql" / "Library" / "Initial",
        initialization_scripts=["initialize_library.sh"],
        provided_schemas=["library_catalog", "library_identity", "library_activity"],
    ),
    DbInitialization(
        source_path=_TEST_SCHEMAS_ROOT / "MySql" / "ExtraTest" / "Initial",
        initialization_scripts=["initialize_db.sh"],
        provided_schemas=["extra_test"],
    ),
]

_pg_projects: list[DbInitialization] = [
    DbInitialization(
        source_path=_TEST_SCHEMAS_ROOT / "Postgres" / "Library" / "Initial",
        initialization_scripts=["initialize_library.sh"],
        provided_schemas=["library_catalog", "library_identity", "library_activity"],
    ),
]


@attrs.define
class RuntimeConfig:
    """Configuration for running code generation."""

    dialect: SqlDialect
    container: dbc.DbContainerBase
    project_config: pc.ProjectConfig
    type_mapper: dt.TypeMapper
    inits: list[DbInitialization]


def _find_projects(
    projects: list[DbInitialization], schemas: list[str]
) -> list[DbInitialization]:
    if len(schemas) != len(set(schemas)):
        raise ValueError("Schema names must be distinct")
    available = {schema for project in projects for schema in project.provided_schemas}
    unknown = set(schemas) - available
    if unknown:
        raise ValueError(f"Unknown schemas: {', '.join(sorted(unknown))}")
    return [
        project
        for project in projects
        if not schemas or any(schema in project.provided_schemas for schema in schemas)
    ]


_IMAGES: dict[SqlDialect, str] = {
    SqlDialect.MYSQL: "mysql:8.4.6",
    SqlDialect.MARIADB: "mariadb:11.8.2",
    SqlDialect.POSTGRES: "postgres:18",
}


def _get_namespaces(project: str) -> tuple[str, str, str]:
    ns_pattern = f"TcfOss.DatabaseManager.{project}.DatabaseComms.EntityFramework"
    data_ns = f"TcfOss.DatabaseManager.{project}.Tests.DatabaseComms"
    return (f"{ns_pattern}.Entities", f"{ns_pattern}.Configurations", data_ns)


def _get_ef_paths(project: str) -> tuple[Path, Path]:
    base_path = (
        REPO_ROOT
        / "src"
        / f"TcfOss.DatabaseManager.{project}"
        / "DatabaseComms"
        / "EntityFramework"
    )
    return (base_path / "Entities", base_path / "Configurations")


def _get_test_data_path(project: str) -> tuple[Path, str]:
    filepath = (
        REPO_ROOT
        / "test"
        / f"TcfOss.DatabaseManager.{project}.Tests"
        / "DatabaseComms"
        / f"{project}ContextTestData.cs"
    )
    class_name = f"{project}ContextTestData"
    return filepath, class_name


def get_config(dialect: SqlDialect, schemas: list[str]) -> RuntimeConfig:
    project_name = dialect.project_name
    type_mapper: dt.TypeMapper
    tables: list[tc.TableConfig]
    inits: list[DbInitialization]
    image = _IMAGES[dialect]
    container: dbc.DbContainerBase
    catalog: str
    default_schema_predicate_lhs: str
    entity_ns, config_ns, data_ns = _get_namespaces(project_name)
    entity_path, config_path = _get_ef_paths(project_name)
    test_data_path, test_data_class = _get_test_data_path(project_name)

    if dialect == SqlDialect.MYSQL:
        type_mapper = dt.map_mysql_type
        tables = cat_my.MYSQL_CONFIGS
        inits = _find_projects(_mysql_projects, schemas)
        container = dbc.MySqlContainer(
            name="dbman_isgen_mysql",
            image=image,
            port=3651,
            user="root",
            password="",
            db="test_db",
        )
        catalog = "def"
        default_schema_predicate_lhs = "t.table_schema"
    elif dialect == SqlDialect.MARIADB:
        type_mapper = dt.map_mysql_type
        tables = cat_my.MARIADB_CONFIGS
        inits = _find_projects(_mysql_projects, schemas)
        container = dbc.MySqlContainer(
            name="dbman_isgen_mariadb",
            image=image,
            port=3652,
            user="root",
            password="",
            db="test_db",
        )
        catalog = "def"
        default_schema_predicate_lhs = "t.table_schema"
    elif dialect == SqlDialect.POSTGRES:
        type_mapper = dt.map_postgres_type
        tables = cat_pg.POSTGRES_CONFIGS
        inits = _find_projects(_pg_projects, schemas)
        container = dbc.PostgresContainer(
            name="dbman_isgen_postgres",
            image=image,
            port=3653,
            user="data",
            password="password",
            db="postgres",
        )
        catalog = "postgres"
        default_schema_predicate_lhs = "n.nspname"
    else:
        raise ValueError(f"Unsupported dialect: {dialect}")

    conf = pc.ProjectConfig(
        dialect=dialect,
        catalog=catalog,
        default_schema_predicate_lhs=default_schema_predicate_lhs,
        entity_namespace=entity_ns,
        config_namespace=config_ns,
        tables=tables,
        schemas=list(schemas) if schemas else [
            schema for item in inits for schema in item.provided_schemas
        ],
        entity_dir=entity_path,
        config_dir=config_path,
        data_namespace=data_ns,
        data_classname=test_data_class,
        data_file=test_data_path,
    )

    return RuntimeConfig(
        dialect=dialect,
        container=container,
        project_config=conf,
        type_mapper=type_mapper,
        inits=inits,
    )
