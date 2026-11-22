"""SQL type mapping and C# literal formatting for selected system columns."""

from abc import ABC, abstractmethod
import datetime
from decimal import Decimal, InvalidOperation
import json
import math
from typing import Callable

import attrs

def cs_string_literal(value: str) -> str:
    """Escape a string for a regular C# string literal."""
    return json.dumps(value, ensure_ascii=True)


@attrs.frozen
class TypeQueryKey:
    """Metadata needed to map a system column, without resolving arbitrary types."""

    db_type: str
    nullable: bool
    column_type: str | None = None
    udt_name: str | None = None
    udt_schema: str | None = None


@attrs.frozen
class DataTypeBase(ABC):
    """An immutable type description and formatter."""

    cs_name: str
    db_name: str
    nullable: bool

    def format_value(self, value: object) -> str:
        if value is None:
            if self.nullable:
                return "null"
            raise ValueError(f"NULL value for nonnullable {self.db_name} field")
        return self._format_value(value)

    @abstractmethod
    def _format_value(self, value: object) -> str:
        """Format a nonnull database value."""

    @property
    def is_reference_type(self) -> bool:
        return self.cs_name == "string" or self.cs_name.endswith("[]")

    @property
    def cs_model_declaration(self) -> str:
        return self.cs_name + ("?" if self.nullable else "")

    @property
    def cs_config_declaration(self) -> str:
        return f".HasColumnType({cs_string_literal(self.db_name)})"


type TypeMapper = Callable[[TypeQueryKey], DataTypeBase]


class StringDataType(DataTypeBase):
    def _format_value(self, value: object) -> str:
        if not isinstance(value, str):
            raise ValueError(f"Expected string for {self.db_name}, got {value!r}")
        return cs_string_literal(value)


class CharDataType(DataTypeBase):
    def _format_value(self, value: object) -> str:
        # PostgreSQL's internal "char" output represents the zero byte as "".
        if self.db_name == '"char"' and value == "":
            value = "\0"
        if not isinstance(value, str) or len(value) != 1 or ord(value) > 0xFFFF:
            raise ValueError(f"Expected one UTF-16 character, got {value!r}")
        escaped = cs_string_literal(value)[1:-1].replace("'", "\\'")
        return f"'{escaped}'"


_INTEGER_RANGES = {
    "sbyte": (-(2**7), 2**7 - 1),
    "byte": (0, 2**8 - 1),
    "short": (-(2**15), 2**15 - 1),
    "ushort": (0, 2**16 - 1),
    "int": (-(2**31), 2**31 - 1),
    "uint": (0, 2**32 - 1),
    "long": (-(2**63), 2**63 - 1),
    "ulong": (0, 2**64 - 1),
}


class NumericDataType(DataTypeBase):
    def _format_value(self, value: object) -> str:
        if isinstance(value, bool) or not isinstance(value, (int, float, Decimal, str)):
            raise ValueError(f"Expected number for {self.db_name}, got {value!r}")

        if self.cs_name in ("float", "double"):
            number = float(value)
            if math.isnan(number):
                return f"{self.cs_name}.NaN"
            if math.isinf(number):
                sign = "Negative" if number < 0 else "Positive"
                return f"{self.cs_name}.{sign}Infinity"
            return repr(number) + ("f" if self.cs_name == "float" else "d")

        try:
            number_decimal = Decimal(str(value))
        except InvalidOperation as error:
            raise ValueError(f"Invalid number for {self.db_name}: {value!r}") from error

        if not number_decimal.is_finite():
            raise ValueError(f"Nonfinite number for {self.cs_name}: {value!r}")

        if self.cs_name == "decimal":
            return f"{number_decimal}m"

        lower, upper = _INTEGER_RANGES[self.cs_name]
        if (
            number_decimal != number_decimal.to_integral_value()
            or not lower <= number_decimal <= upper
        ):
            raise ValueError(f"Value outside {self.cs_name} range: {value!r}")

        suffix = {"long": "L", "uint": "U", "ulong": "UL"}.get(self.cs_name, "")
        return str(int(number_decimal)) + suffix


class BooleanDataType(DataTypeBase):
    def _format_value(self, value: object) -> str:
        text = str(value).lower()
        if text in ("true", "t", "1"):
            return "true"
        if text in ("false", "f", "0"):
            return "false"
        raise ValueError(f"Invalid Boolean value: {value!r}")


class TemporalDataType(DataTypeBase):
    def _format_value(self, value: object) -> str:
        if self.cs_name == "System.DateTime" and isinstance(value, datetime.datetime):
            return (
                f"System.DateTime.Parse({cs_string_literal(value.isoformat())}, "
                "System.Globalization.CultureInfo.InvariantCulture, "
                "System.Globalization.DateTimeStyles.RoundtripKind)"
            )
        if self.cs_name == "System.DateOnly" and isinstance(value, datetime.date):
            return (
                f"System.DateOnly.Parse({cs_string_literal(value.isoformat())}, "
                "System.Globalization.CultureInfo.InvariantCulture)"
            )
        if self.cs_name == "System.TimeOnly" and isinstance(value, datetime.time):
            return (
                f"System.TimeOnly.Parse({cs_string_literal(value.isoformat())}, "
                "System.Globalization.CultureInfo.InvariantCulture)"
            )
        raise ValueError(f"Invalid value for {self.cs_name}: {value!r}")


@attrs.frozen
class ArrayDataType(DataTypeBase):
    element_type: DataTypeBase

    def _format_value(self, value: object) -> str:
        if self.db_name in ("oidvector", "int2vector") and isinstance(value, str):
            value = value.split()
        if self.cs_name == "byte[]" and isinstance(value, (bytes, bytearray, memoryview)):
            value = list(bytes(value))
        if not isinstance(value, (list, tuple)):
            raise ValueError(f"Expected array for {self.db_name}, got {value!r}")
        return "[" + ", ".join(self.element_type.format_value(item) for item in value) + "]"


def get_type(cs_name: str, db_name: str, nullable: bool) -> DataTypeBase:
    if cs_name == "string":
        return StringDataType(cs_name, db_name, nullable)
    if cs_name in _INTEGER_RANGES or cs_name in ("float", "double", "decimal"):
        return NumericDataType(cs_name, db_name, nullable)
    if cs_name == "bool":
        return BooleanDataType(cs_name, db_name, nullable)
    if cs_name == "char":
        return CharDataType(cs_name, db_name, nullable)
    if cs_name in ("System.DateTime", "System.DateOnly", "System.TimeOnly"):
        return TemporalDataType(cs_name, db_name, nullable)
    raise ValueError(f"Unsupported C# data type: {cs_name}")


_STANDARD_TYPES = {
    "char": "string",
    "character": "string",
    "varchar": "string",
    "character varying": "string",
    "text": "string",
    "name": "string",
    "longtext": "string",
    "mediumtext": "string",
    "tinytext": "string",
    "int": "int",
    "integer": "int",
    "int4": "int",
    "bigint": "long",
    "int8": "long",
    "smallint": "short",
    "int2": "short",
    "boolean": "bool",
    "bool": "bool",
    "real": "float",
    "float4": "float",
    "double precision": "double",
    "float8": "double",
    "decimal": "decimal",
    "numeric": "decimal",
    '"char"': "char",
    "oid": "uint",
    "xid": "uint",
    "cid": "uint",
    "xid8": "ulong",
    "regproc": "uint",
    "pg_node_tree": "string",
    "date": "System.DateOnly",
    "datetime": "System.DateTime",
    "timestamp": "System.DateTime",
    "timestamp without time zone": "System.DateTime",
    "timestamp with time zone": "System.DateTime",
    "time without time zone": "System.TimeOnly",
}


def map_standard_type(key: TypeQueryKey) -> DataTypeBase:
    db_type = key.db_type.lower()
    cs_name = _STANDARD_TYPES.get(db_type)
    if cs_name is None:
        raise ValueError(f"Unsupported system-column data type: {db_type}")
    return get_type(cs_name, key.column_type or db_type, key.nullable)


_POSTGRES_ARRAY_TYPES = {
    "_int2": ("smallint", "short"),
    "_int4": ("integer", "int"),
    "_int8": ("bigint", "long"),
    "_oid": ("oid", "uint"),
    "_text": ("text", "string"),
    "_name": ("name", "string"),
    "_char": ('"char"', "char"),
    "_bool": ("boolean", "bool"),
    "_float4": ("real", "float"),
    "_float8": ("double precision", "double"),
    "_numeric": ("numeric", "decimal"),
    "int2vector": ("smallint", "short"),
    "oidvector": ("oid", "uint"),
}


def map_postgres_type(key: TypeQueryKey) -> DataTypeBase:
    if key.udt_schema == "pg_catalog" and key.udt_name == "_aclitem":
        return get_type("string", "aclitem[]", key.nullable)
    if key.db_type.lower() == "anyarray":
        return get_type("string", "anyarray", key.nullable)
    if key.db_type.lower() == "array" or key.udt_name in ("int2vector", "oidvector"):
        if key.udt_schema != "pg_catalog" or key.udt_name not in _POSTGRES_ARRAY_TYPES:
            raise ValueError(
                f"Unsupported PostgreSQL system array: {key.udt_schema}.{key.udt_name}"
            )

        element_db, element_cs = _POSTGRES_ARRAY_TYPES[key.udt_name]
        db_name = (
            key.udt_name if key.udt_name in ("int2vector", "oidvector") else element_db + "[]"
        )
        return ArrayDataType(
            element_cs + "[]",
            db_name,
            key.nullable,
            get_type(element_cs, element_db, False),
        )

    if key.db_type.lower() == "bytea":
        return ArrayDataType("byte[]", "bytea", key.nullable, get_type("byte", "byte", False))

    return map_standard_type(key)


def map_mysql_type(key: TypeQueryKey) -> DataTypeBase:
    db_type = key.db_type.lower()
    cs_name = {
        "tinyint": "sbyte",
        "smallint": "short",
        "mediumint": "int",
        "int": "int",
        "integer": "int",
        "bigint": "long",
        "float": "float",
        "double": "double",
        "enum": "string",
        "set": "string",
    }.get(db_type)

    if cs_name is None:
        return map_standard_type(key)

    if key.column_type and "unsigned" in key.column_type.lower().split():
        cs_name = {"sbyte": "byte", "short": "ushort", "int": "uint", "long": "ulong"}.get(
            cs_name, cs_name
        )

    return get_type(cs_name, key.column_type or db_type, key.nullable)
