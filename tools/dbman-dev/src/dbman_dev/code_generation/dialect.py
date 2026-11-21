"""Database dialect enumeration."""

from __future__ import annotations

from enum import Enum


class SqlDialect(Enum):
    POSTGRES = "postgres"
    MYSQL = "mysql"
    MARIADB = "mariadb"

    @staticmethod
    def parse(value: str) -> SqlDialect:
        """Return a dialect from a string"""

        value = value.casefold()
        if value == "postgres":
            return SqlDialect.POSTGRES
        if value == "mysql":
            return SqlDialect.MYSQL
        if value == "mariadb":
            return SqlDialect.MARIADB
        raise ValueError(f"Unknown SQL dialect: {value}")

    @property
    def case_sensitive(self) -> bool:
        """Return whether the dialect is case-sensitive."""
        if self == SqlDialect.POSTGRES:
            return True
        if self == SqlDialect.MYSQL:
            return False
        if self == SqlDialect.MARIADB:
            return False
        raise ValueError(f"Unknown SQL dialect: {self}")

    @property
    def extra_info_schema_columns(self) -> str:
        """Return the extra info schema columns for the dialect."""
        if self == SqlDialect.POSTGRES:
            return ", udt_name, udt_schema"
        if self == SqlDialect.MYSQL:
            return ", column_type"
        if self == SqlDialect.MARIADB:
            return ", column_type"
        raise ValueError(f"Unknown SQL dialect: {self}")

    @property
    def project_name(self) -> str:
        """Return the project name for the dialect."""
        if self == SqlDialect.POSTGRES:
            return "Postgres"
        if self == SqlDialect.MYSQL:
            return "MySql"
        if self == SqlDialect.MARIADB:
            return "MariaDb"
        raise ValueError(f"Unknown SQL dialect: {self}")
