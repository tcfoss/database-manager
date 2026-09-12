"""Database default values."""

MYSQL_IMAGE = "mysql:8.4.6"
MARIADB_IMAGE = "mariadb:11.8.2"

ENVIRONMENT = {
    "MYSQL_ALLOW_EMPTY_PASSWORD": "true",
    "MYSQL_DATABASE": "test_db",
    "MYSQL_USER": "data",
    "MYSQL_PASSWORD": "password",
}

SCHEMAS = ["library_catalog", "library_identity", "library_activity"]
