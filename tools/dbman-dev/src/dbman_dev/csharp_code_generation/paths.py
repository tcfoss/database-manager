"""Path utilities."""

from dbman_dev.paths import REPO_ROOT

PROJECT_ROOT = REPO_ROOT

MYSQL_ENTITY_FRAMEWORK_DIR = (
    REPO_ROOT / "src" / "TcfOss.DatabaseManager.MySql" / "DatabaseComms" / "EntityFramework"
)
MARIADB_ENTITY_FRAMEWORK_DIR = (
    REPO_ROOT / "src" / "TcfOss.DatabaseManager.MariaDb" / "DatabaseComms" / "EntityFramework"
)
POSTGRES_ENTITY_FRAMEWORK_DIR = (
    REPO_ROOT / "src" / "TcfOss.DatabaseManager.Postgres" / "DatabaseComms" / "EntityFramework"
)

MYSQL_TEST_PROJECTS_DIR = REPO_ROOT / "test" / "Resources" / "TestSchemas" / "MySql"

MYSQL_TEST_DATA = (
    REPO_ROOT
    / "test"
    / "TcfOss.DatabaseManager.MySql.Tests"
    / "DatabaseComms"
    / "MySqlContextTestData.cs"
)
POSTGRES_TEST_PROJECTS_DIR = REPO_ROOT / "test" / "Resources" / "TestSchemas" / "Postgres"
POSTGRES_TEST_DATA = (
    REPO_ROOT
    / "test"
    / "TcfOss.DatabaseManager.Postgres.Tests"
    / "DatabaseComms"
    / "PostgresContextTestData.cs"
)

MARIADB_TEST_DATA = (
    REPO_ROOT
    / "test"
    / "TcfOss.DatabaseManager.MariaDb.Tests"
    / "DatabaseComms"
    / "MariaDbContextTestData.cs"
)
