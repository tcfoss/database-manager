"""Discover and resolve the selected system-table columns."""

from __future__ import annotations

import logging
import re

import attrs

import dbman_dev.code_generation.data_types as dt
import dbman_dev.code_generation.containers as dbc
from dbman_dev.code_generation.dialect import SqlDialect
import dbman_dev.code_generation.table_config as tc
import dbman_dev.utilities.text_conversion as conv

logger = logging.getLogger(__name__)


class StructureQueryer:
    def __init__(self, dialect: SqlDialect, catalog: str, type_mapper: dt.TypeMapper):
        self.dialect = dialect
        self.catalog = catalog
        self.type_mapper = type_mapper

    def get_model(self, con: dbc.DbContainerBase, table: tc.TableConfig) -> tc.ModelConfig:
        case_sensitive = self.dialect.case_sensitive
        col_configs = table.get_col_dict(case_sensitive)
        query, params = self._build_table_query(table)
        logger.info("Executing query: %s", query)
        raw_results = con.query_dict(query, params)
        fields: list[tc.FieldConfig] = []
        discovered: set[str] = set()
        for row in raw_results:
            if not self.dialect.case_sensitive:
                row = {k.casefold(): v for k, v in row.items()}
            col_info = _ColumnInfo.from_dict(row)
            key = col_info.name if case_sensitive else col_info.name.casefold()
            discovered.add(key)
            col_config = col_configs.get(key)
            if table.column_configs is not None and (col_config is None or col_config.ignored):
                continue
            fields.append(self._map_to_field(col_config, col_info, table))
            logger.debug("Detected field: %s", fields[-1])

        missing_columns = col_configs.keys() - discovered
        if missing_columns:
            raise ValueError(
                f"{table.table_name}: columns not discovered: {sorted(missing_columns)}"
            )
        if not fields:
            raise ValueError(f"{table.schema_name}.{table.table_name}: no selected columns")

        properties = [field.property_name for field in fields]
        if len(set(properties)) != len(properties):
            raise ValueError(f"{table.table_name}: duplicate resolved property names")
        key_properties = table.key_properties or []
        if len(set(key_properties)) != len(key_properties):
            raise ValueError(f"{table.table_name}: duplicate key properties")
        missing_keys = set(key_properties) - set(properties)
        if missing_keys:
            raise ValueError(
                f"{table.table_name}: key properties not selected: {sorted(missing_keys)}"
            )

        base_name = table.base_name_override or conv.snake_to_pascal(table.table_name)
        for name in [base_name, *properties]:
            if not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", name):
                raise ValueError(f"{table.table_name}: invalid C# identifier {name!r}")
        return tc.ModelConfig(
            table_name=table.table_name,
            schema_name=table.schema_name,
            model_name=f"{base_name}Entity",
            config_name=f"{base_name}Config",
            fields=fields,
            key_properties=table.key_properties,
            schemas_join=table.schemas_join,
            filter_by_schema=table.filter_by_schema,
            additional_predicate=table.additional_predicate,
            order_by=table.order_by,
            data_query_template=table.data_query_template,
        )

    def _build_table_query(self, table: tc.TableConfig) -> tuple[str, tuple[object, ...]]:
        extra = self.dialect.extra_info_schema_columns
        query = (
            "SELECT column_name, data_type, is_nullable,"
            " character_maximum_length, numeric_precision, numeric_scale"
            f"{extra} FROM information_schema.columns"
            " WHERE table_catalog = %s AND table_schema = %s AND table_name = %s"
            " ORDER BY ordinal_position"
        )
        return query, (self.catalog, table.schema_name, table.table_name)

    def _map_to_field(
        self,
        col_config: tc.ColumnConfig | None,
        col_info: _ColumnInfo,
        table: tc.TableConfig,
    ) -> tc.FieldConfig:

        property_name = (
            col_config.property_name_override
            if col_config and col_config.property_name_override is not None
            else conv.snake_to_pascal(col_info.name)
        )

        nullable = (
            col_config.nullable_override
            if col_config and col_config.nullable_override is not None
            else col_info.is_nullable
        )

        if property_name in (table.key_properties or []):
            nullable = False

        try:
            data_type = self.type_mapper(
                dt.TypeQueryKey(
                    col_info.data_type,
                    nullable,
                    col_info.column_type,
                    col_info.udt_name,
                    col_info.udt_schema,
                )
            )
        except ValueError as error:
            raise ValueError(f"{table.table_name}.{col_info.name}: {error}") from error

        return tc.FieldConfig(
            column_name=col_info.name,
            property_name=property_name,
            data_type=data_type,
            max_length=col_info.char_max_len,
            numeric_precision=col_info.num_precision,
            numeric_scale=col_info.num_scale,
            query_template=col_config.query_template if col_config else None,
        )


@attrs.frozen
class _ColumnInfo:
    name: str
    data_type: str
    is_nullable: bool
    char_max_len: int | None = None
    num_precision: int | None = None
    num_scale: int | None = None
    column_type: str | None = None
    udt_name: str | None = None
    udt_schema: str | None = None

    @staticmethod
    def from_dict(data: dict[str, object]) -> _ColumnInfo:
        def text(key: str) -> str:
            value = data[key]
            if not isinstance(value, str):
                raise ValueError(f"Invalid metadata for {key}: {value!r}")
            return value

        def optional_text(key: str) -> str | None:
            value = data.get(key)
            if value is not None and not isinstance(value, str):
                raise ValueError(f"Invalid metadata for {key}: {value!r}")
            return value

        def optional_int(key: str) -> int | None:
            value = data[key]
            if value is not None and (isinstance(value, bool) or not isinstance(value, int)):
                raise ValueError(f"Invalid metadata for {key}: {value!r}")
            return value

        nullable = text("is_nullable")
        if nullable not in ("YES", "NO"):
            raise ValueError(f"Invalid nullability metadata: {nullable!r}")

        return _ColumnInfo(
            name=text("column_name"),
            data_type=text("data_type"),
            is_nullable=nullable == "YES",
            char_max_len=optional_int("character_maximum_length"),
            num_precision=optional_int("numeric_precision"),
            num_scale=optional_int("numeric_scale"),
            column_type=optional_text("column_type"),
            udt_name=optional_text("udt_name"),
            udt_schema=optional_text("udt_schema"),
        )
