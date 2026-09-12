"""Path utilities."""

from dbman_dev.paths import REPO_ROOT

PROJECT_ROOT = REPO_ROOT

MYSQL_ENTITY_FRAMEWORK_DIR = (
    REPO_ROOT / "src" / "TcfOss.DatabaseManager.MySql" / "DatabaseComms" / "EntityFramework"
)
MARIADB_ENTITY_FRAMEWORK_DIR = (
    REPO_ROOT / "src" / "TcfOss.DatabaseManager.MariaDb" / "DatabaseComms" / "EntityFramework"
)

MYSQL_TEST_SCHEMA_DIR = REPO_ROOT / "test" / "Resources" / "TestSchemas" / "MySql" / "Initial"

MYSQL_TEST_DATA = (
    REPO_ROOT
    / "test"
    / "TcfOss.DatabaseManager.MySql.Tests"
    / "DatabaseComms"
    / "MySqlContextTestData.cs"
)

MARIADB_TEST_DATA = (
    REPO_ROOT
    / "test"
    / "TcfOss.DatabaseManager.MariaDb.Tests"
    / "DatabaseComms"
    / "MariaDbContextTestData.cs"
)
