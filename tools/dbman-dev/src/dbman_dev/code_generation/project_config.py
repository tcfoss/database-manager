"""Configuration for a project."""

import pathlib
import attrs

from dbman_dev.code_generation.dialect import SqlDialect
import dbman_dev.code_generation.table_config as tc


@attrs.define
class ProjectConfig:
    """Configuration for a project."""

    dialect: SqlDialect

    catalog: str
    default_schema_predicate_lhs: str

    entity_namespace: str
    config_namespace: str
    tables: list[tc.TableConfig]
    schemas: list[str]

    entity_dir: pathlib.Path | None
    config_dir: pathlib.Path | None

    data_namespace: str
    data_classname: str
    data_file: pathlib.Path | None
