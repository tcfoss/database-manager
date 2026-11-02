"""Read PostgreSQL catalog columns for C# model generation."""

from __future__ import annotations

import datetime
import json
import re
from decimal import Decimal

from dbman_dev.containers.db_container import DbContainer
from dbman_dev.csharp_code_generation.entity_model import EntityModel

_LENGTH_TYPE = re.compile(r"^(character varying|character)\((\d+)\)$")
_NUMERIC_TYPE = re.compile(r"^numeric(?:\((\d+)(?:,(\d+))?\))?$")
_INTEGRAL_TYPES = {
    "smallint": "short",
    "integer": "int",
    "bigint": "long",
    "oid": "uint",
    "xid": "uint",
    "cid": "uint",
}
_ARRAY_TYPES = {
    "smallint": "short",
    "integer": "int",
    "bigint": "long",
    "oid": "uint",
    "text": "string",
}


class PostgresEntityModel(EntityModel):
    """Entity model with PostgreSQL-aware runtime value formatting."""

    def get_value(self, given_value) -> str:
        """Return a C# initializer for a psycopg catalog value."""
        if self.is_nullable and given_value is None:
            return "null"
        if isinstance(given_value, bool):
            return "true" if given_value else "false"
        if isinstance(given_value, datetime.datetime):
            return f"DateTime.Parse({json.dumps(given_value.isoformat())})"
        if isinstance(given_value, datetime.date):
            return f"DateOnly.Parse({json.dumps(given_value.isoformat())})"
        if isinstance(given_value, datetime.time):
            return f"TimeOnly.Parse({json.dumps(given_value.isoformat())})"
        if isinstance(given_value, Decimal):
            return f"{given_value}m"
        if isinstance(given_value, float):
            literal = repr(given_value)
            return f"{literal}f" if self.data_type == "float" else literal
        if isinstance(given_value, (list, tuple)):
            if self.data_type == "string":
                return json.dumps(_postgres_array_text(given_value), ensure_ascii=True)
            return "[" + ", ".join(self._format_array_value(value) for value in given_value) + "]"
        if isinstance(given_value, str) and self.data_type in {
            "short",
            "int",
            "long",
            "uint",
        }:
            return str(int(given_value))
        if isinstance(given_value, str):
            return json.dumps(given_value, ensure_ascii=True)
        if self.data_type == "string":
            return json.dumps(str(given_value), ensure_ascii=True)
        return str(given_value)

    @staticmethod
    def _format_array_value(value) -> str:
        if value is None:
            return "null"
        if isinstance(value, bool):
            return "true" if value else "false"
        if isinstance(value, str):
            return json.dumps(value, ensure_ascii=True)
        return str(value)


def get_postgres_entity_models(
    container: DbContainer, schema: str, table: str
) -> list[EntityModel]:
    """Return mapped PostgreSQL columns ordered by physical attribute position."""
    schema_literal = "'" + schema.replace("'", "''") + "'"
    table_literal = "'" + table.replace("'", "''") + "'"
    rows = container.query(
        "SELECT a.attname, pg_catalog.format_type(a.atttypid, a.atttypmod), "
        "NOT a.attnotnull "
        "FROM pg_catalog.pg_attribute AS a "
        "JOIN pg_catalog.pg_class AS c ON c.oid = a.attrelid "
        "JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace "
        f"WHERE n.nspname = {schema_literal} AND c.relname = {table_literal} "
        "AND a.attnum > 0 AND NOT a.attisdropped ORDER BY a.attnum"
    )
    return [
        _to_entity_model(column_name, data_type, nullable)
        for column_name, data_type, nullable in rows
    ]


def _to_entity_model(column_name: str, sql_type: str, nullable: bool) -> PostgresEntityModel:
    """Map a PostgreSQL formatted type to an appropriate C# property type."""
    data_type = "string"
    match = _LENGTH_TYPE.match(sql_type)
    if match:
        data_type = "string"
    elif numeric_match := _NUMERIC_TYPE.match(sql_type):
        data_type = "decimal"
    elif sql_type in _INTEGRAL_TYPES:
        data_type = _INTEGRAL_TYPES[sql_type]
    elif sql_type == "boolean":
        data_type = "bool"
    elif sql_type == "real":
        data_type = "float"
    elif sql_type == "double precision":
        data_type = "double"
    elif sql_type == "date":
        data_type = "DateOnly"
    elif sql_type.startswith("timestamp"):
        data_type = "DateTime"
    elif sql_type.startswith("time"):
        data_type = "TimeOnly"
    elif sql_type.endswith("[]"):
        element_type = sql_type[:-2]
        array_element = _ARRAY_TYPES.get(element_type)
        if array_element:
            data_type = f"{array_element}[]"

    if match:
        data_type = "string"
    elif numeric_match:
        data_type = "decimal"

    return PostgresEntityModel(
        column_name=column_name,
        property_name=_to_pascal_case(column_name),
        data_type=data_type,
        sql_type=sql_type,
        is_nullable=nullable,
    )


def _postgres_array_text(values: list | tuple) -> str:
    def format_element(value) -> str:
        if value is None:
            return "NULL"
        if isinstance(value, (list, tuple)):
            return _postgres_array_text(value)
        text = str(value)
        needs_quotes = (
            not text
            or text.upper() == "NULL"
            or any(character in ',{}"\\' for character in text)
            or any(character.isspace() for character in text)
        )
        if needs_quotes:
            return '"' + text.replace("\\", "\\\\").replace('"', '\\"') + '"'
        return text

    return "{" + ",".join(format_element(value) for value in values) + "}"


def _to_pascal_case(value: str) -> str:
    return "".join(part[:1].upper() + part[1:] for part in value.split("_"))
