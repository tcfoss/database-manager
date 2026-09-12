"""Models for database objects."""

from __future__ import annotations

import re

import attrs
from dbman_dev.csharp_code_generation import entity_model as _em
from dbman_dev.utilities import text_conversion

_LEN_PAT = re.compile(r"\((\d+)\)")
_PREC_PAT = re.compile(r"\((\d+)\s*,\s*(\d+)\)")
_INTEGRAL_PAT = re.compile(
    r"^(int|bigint|smallint|tinyint|mediumint).*?(unsigned)?$", re.IGNORECASE
)


@attrs.define
class ShowFieldsResult:
    """Result of SHOW FIELDS query."""

    field: str
    data_type: str
    nullable: bool
    key: str
    default: str | None
    extra: str

    def to_cs_model(self) -> _em.EntityModel:
        """Convert to C# entity model."""

        if self.data_type.startswith("varchar"):
            return self.__to_varchar_model()

        if self.data_type.startswith("char"):
            return self.__to_char_model()

        if integral_match := _INTEGRAL_PAT.match(self.data_type):
            specific_type = integral_match.group(1).lower()
            unsigned = integral_match.group(2) is not None

            return self.__to_integral_model(specific_type, unsigned)

        if self.data_type.startswith("datetime") or self.data_type.startswith("timestamp"):
            return _em.DateTimeEntityModel(
                column_name=self.field,
                property_name=text_conversion.snake_to_pascal(self.field),
                data_type="DateTime",
                sql_type=self.data_type,
                is_nullable=self.nullable,
            )

        if self.data_type.startswith("decimal"):
            return self.__to_decimal_model()

        return _em.EntityModel(
            column_name=self.field,
            property_name=text_conversion.snake_to_pascal(self.field),
            data_type="string",  # Default to string, can be adjusted based on data_type
            sql_type=self.data_type,
            is_nullable=self.nullable,
        )

    def __to_varchar_model(self) -> _em.VarcharEntityModel:
        """Convert to VARCHAR C# model."""
        match = _LEN_PAT.search(self.data_type)
        if not match:
            raise ValueError(f"Invalid VARCHAR length in {self}")
        max_length = int(match.group(1))
        return _em.VarcharEntityModel(
            column_name=self.field,
            property_name=text_conversion.snake_to_pascal(self.field),
            data_type="string",
            sql_type=self.data_type,
            is_nullable=self.nullable,
            max_length=max_length,
        )

    def __to_char_model(self) -> _em.CharEntityModel:
        """Convert to CHAR C# model."""
        match = _LEN_PAT.search(self.data_type)
        if not match:
            raise ValueError(f"Invalid CHAR length in {self}")
        max_length = int(match.group(1))
        return _em.CharEntityModel(
            column_name=self.field,
            property_name=text_conversion.snake_to_pascal(self.field),
            data_type="string",
            sql_type=self.data_type,
            is_nullable=self.nullable,
            max_length=max_length,
        )

    def __to_integral_model(self, specific_type: str, unsigned: bool) -> _em.IntEntityModel:
        """Convert to integral C# model."""
        if specific_type in ("int", "mediumint"):
            specific_type = "int"
        elif specific_type == "bigint":
            specific_type = "long"
        elif specific_type == "smallint":
            specific_type = "short"
        elif specific_type == "tinyint":
            specific_type = "byte"
        else:
            raise ValueError(f"Unsupported integral type: {specific_type}")

        if unsigned and specific_type in ("int", "long", "short"):
            specific_type = "u" + specific_type
        elif not unsigned and specific_type == "byte":
            specific_type = "s" + specific_type

        return _em.IntEntityModel(
            column_name=self.field,
            property_name=text_conversion.snake_to_pascal(self.field),
            data_type=specific_type,
            sql_type=self.data_type,
            is_nullable=self.nullable,
        )

    def __to_decimal_model(self) -> _em.DecimalEntityModel:
        """Convert to decimal C# model."""
        if match := _PREC_PAT.search(self.data_type):
            precision = int(match.group(1))
            scale = int(match.group(2))
        elif match := _LEN_PAT.search(self.data_type):
            precision = int(match.group(1))
            scale = 0
        else:
            precision = 10
            scale = 0

        return _em.DecimalEntityModel(
            column_name=self.field,
            property_name=text_conversion.snake_to_pascal(self.field),
            data_type="decimal",
            sql_type=self.data_type,
            is_nullable=self.nullable,
            precision=precision,
            scale=scale,
        )

    @classmethod
    def from_row(cls, row: tuple) -> ShowFieldsResult:
        """Create a ShowFieldsResult from a database row."""
        return cls(
            field=row[0],
            data_type=row[1],
            nullable=row[2] == "YES",
            key=row[3],
            default=row[4],
            extra=row[5],
        )
