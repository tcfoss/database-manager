"""Models for C# code generation."""

import datetime
import attrs

_CONFIG_LINE_SEP = "\n            "


@attrs.define
class EntityModel:
    """Model for an entity in C# code generation."""

    column_name: str
    property_name: str
    data_type: str
    sql_type: str | None
    is_nullable: bool

    def get_entity_line(self) -> str:
        """Generate the C# line for this entity."""

        required = "required " if not self.is_nullable else ""
        nullable_suffix = "?" if self.is_nullable else ""
        return (
            f"public {required}{self.data_type}{nullable_suffix} "
            f"{self.property_name} {{ get; set; }}"
        )

    def get_config_line(self) -> str:
        """Generate the C# line for this entity in the configuration."""

        result = (
            f"builder.Property(e => e.{self.property_name})"
            f'{_CONFIG_LINE_SEP}.HasColumnName("{self.column_name}")'
        )
        if self.sql_type:
            result += f'{_CONFIG_LINE_SEP}.HasColumnType("{self.sql_type}")'
        return result + ";"

    def get_value(self, given_value) -> str:
        """Get the C# representation of the given value."""

        if self.is_nullable and (given_value is None or given_value.upper() == "NULL"):
            return "null"
        value = given_value.strip().replace('"', r"\"").replace("\n", "\\n").replace("\r", "\\r")
        return f'"{value}"'


@attrs.define
class IntEntityModel(EntityModel):
    """Model for an INT entity in C# code generation."""

    def get_config_line(self) -> str:
        """Generate the C# line for this INT entity in the configuration."""

        # C# can figure out the data type.
        result = (
            f"builder.Property(e => e.{self.property_name})"
            f'{_CONFIG_LINE_SEP}.HasColumnName("{self.column_name}");'
        )
        return result

    def get_value(self, given_value) -> str:
        if isinstance(given_value, int):
            return str(given_value)
        if self.is_nullable and (
            given_value is None or (isinstance(given_value, str) and given_value.upper() == "NULL")
        ):
            return "null"
        return given_value.strip()


@attrs.define
class VarcharEntityModel(EntityModel):
    """Model for a VARCHAR entity in C# code generation."""

    max_length: int

    def get_config_line(self) -> str:
        """Generate the C# line for this VARCHAR entity in the configuration."""
        is_required = "true" if not self.is_nullable else "false"
        return (
            f"builder.VarcharProperty(e => e.{self.property_name}, "
            f'"{self.column_name}", {self.max_length}, isRequired: {is_required});'
        )


@attrs.define
class CharEntityModel(EntityModel):
    """Model for a CHAR entity in C# code generation."""

    max_length: int

    def get_config_line(self) -> str:
        """Generate the C# line for this CHAR entity in the configuration."""
        is_required = "true" if not self.is_nullable else "false"
        return (
            f"builder.CharProperty(e => e.{self.property_name}, "
            f'"{self.column_name}", {self.max_length}, isRequired: {is_required});'
        )


@attrs.define
class DecimalEntityModel(EntityModel):
    """Model for a DECIMAL entity in C# code generation."""

    precision: int
    scale: int | None = None

    def get_config_line(self) -> str:
        """Generate the C# line for this DECIMAL entity in the configuration."""
        is_required = "true" if not self.is_nullable else "false"
        return (
            f"builder.DecimalProperty(e => e.{self.property_name}, "
            f'"{self.column_name}", {self.precision}, {self.scale}, isRequired: {is_required});'
        )

    def get_value(self, given_value) -> str:
        if isinstance(given_value, float):
            return str(given_value)
        if self.is_nullable and (given_value is None or given_value.upper() == "NULL"):
            return "null"
        return given_value.strip()


@attrs.define
class DateTimeEntityModel(EntityModel):
    """Model for a DATETIME entity in C# code generation."""

    def get_value(self, given_value) -> str:
        if isinstance(given_value, datetime.datetime):
            return f'DateTime.Parse("{given_value.isoformat()}")'
        if self.is_nullable and (given_value is None or given_value.upper() == "NULL"):
            return "null"
        return f'DateTime.Parse("{given_value}")'
