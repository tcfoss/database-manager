"""Configurations for DB tables and columns."""

import attrs
from dbman_dev.code_generation.data_types import DataTypeBase, cs_string_literal


@attrs.define
class SchemaJoinConfig:
    """Configuration for a join to the table containing schema names."""

    join_clause: str
    schema_predicate_lhs: str | None = None


@attrs.define
class ColumnConfig:
    """Configuration for a single column."""

    column_name: str
    property_name_override: str | None = None
    nullable_override: bool | None = None
    ignored: bool = False
    query_template: str | None = None


@attrs.define
class TableConfig:
    """Configuration for a single table."""

    table_name: str
    schema_name: str
    base_name_override: str | None = None
    key_properties: list[str] | None = None
    column_configs: list[ColumnConfig] | None = None
    schemas_join: SchemaJoinConfig | None = None
    filter_by_schema: bool = True
    additional_predicate: str | None = None
    order_by: list[str] | None = None
    data_query_template: str | None = None

    def get_col_dict(self, case_sensitive: bool = True) -> dict[str, ColumnConfig]:
        """Return a dictionary mapping column names to their configurations."""
        if not self.column_configs:
            return {}
        result: dict[str, ColumnConfig] = {}
        for col in self.column_configs:
            key = col.column_name if case_sensitive else col.column_name.casefold()
            if key in result:
                raise ValueError(
                    f"{self.table_name}: duplicate configured column {col.column_name!r}"
                )
            result[key] = col
        return result


@attrs.define
class FieldConfig:
    """Configuration for a resolved field."""

    column_name: str
    property_name: str
    data_type: DataTypeBase
    max_length: int | None = None
    numeric_precision: int | None = None
    numeric_scale: int | None = None
    query_template: str | None = None

    def format_value(self, value: object) -> str:
        """Format the value according to data type"""

        try:
            return self.data_type.format_value(value)
        except ValueError as error:
            raise ValueError(f"Column {self.column_name!r}: {error}") from error

    @property
    def cs_model_declaration(self) -> str:
        """Return the C# model declaration for the field."""
        required = (
            "required " if self.data_type.is_reference_type and not self.data_type.nullable else ""
        )
        return f"public {required}{self.data_type.cs_model_declaration} {self.property_name} {{ get; set; }}"

    @property
    def cs_config_declarations(self) -> list[str]:
        """Return the C# configuration declarations for the field."""
        lines = [f"builder.Property(e => e.{self.property_name})"]
        lines.append(f".HasColumnName({cs_string_literal(self.column_name)})")
        lines.append(self.data_type.cs_config_declaration)
        bounded_character_type = self.data_type.db_name.lower().split("(", 1)[0].strip() in (
            "char", "varchar", "character", "character varying",
        )
        if self.max_length is not None and bounded_character_type:
            lines.append(f".HasMaxLength({self.max_length})")
        if self.numeric_precision is not None and self.data_type.cs_name == "decimal":
            args = str(self.numeric_precision)
            if self.numeric_scale is not None:
                args += f", {self.numeric_scale}"
            lines.append(f".HasPrecision({args})")
        lines[-1] += ";"
        return lines


@attrs.define
class ModelConfig:
    """Configuration for a resolved model."""

    table_name: str
    schema_name: str
    model_name: str
    config_name: str
    fields: list[FieldConfig]
    key_properties: list[str] | None = None
    schemas_join: SchemaJoinConfig | None = None
    filter_by_schema: bool = True
    additional_predicate: str | None = None
    order_by: list[str] | None = None
    data_query_template: str | None = None

    @property
    def cs_config_to_table(self) -> str:
        """Return the C# configuration table binding."""
        return f"builder.ToTable({cs_string_literal(self.table_name)}, {cs_string_literal(self.schema_name)});"

    @property
    def cs_config_key(self) -> str:
        """Return the C# configuration for the resolved key properties."""
        if not self.key_properties:
            return "builder.HasNoKey();"

        if len(self.key_properties) == 1:
            return f"builder.HasKey(e => e.{self.key_properties[0]});"

        key_properties_str = ", ".join(f"e.{prop}" for prop in self.key_properties)
        return f"builder.HasKey(e => new {{ {key_properties_str} }});"

    def get_initializer_row(self, data: tuple[object, ...]) -> str:
        """Return a C# initializer row for the given data tuple."""

        if len(data) != len(self.fields):
            raise ValueError(
                f"{self.table_name}: expected {len(self.fields)} values, got {len(data)}"
            )
        result = f"new {self.model_name} {{ "
        property_initializers = []
        for f, value in zip(self.fields, data, strict=True):
            formatted_value = f.format_value(value)
            property_initializers.append(f"{f.property_name} = {formatted_value}")

        return result + ", ".join(property_initializers) + " }"
