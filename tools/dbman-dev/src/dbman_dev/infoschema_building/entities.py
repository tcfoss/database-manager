"""Generate C# entities from database tables."""

import argparse
from pathlib import Path
from dbman_dev.db import info_schema, defaults as db_defaults
from dbman_dev.csharp_code_generation.entity_generator import EntityGeneratorConfig, run
from dbman_dev.csharp_code_generation.model_config import ModelConfig
from dbman_dev.cliversion import add_version_argument
from dbman_dev.csharp_code_generation import paths
from dbman_dev.utilities import linq


def _extract_tables(dialect: str, initial_tables: list[str] | None = None) -> list[ModelConfig]:

    if initial_tables is None:
        if dialect == "MySql":
            return list(info_schema.MYSQL_CONFIGS.values())
        if dialect == "MariaDb":
            return list(info_schema.MARIADB_CONFIGS.values())

        raise ValueError(f"Unsupported dialect: {dialect}")

    real_tables: list[ModelConfig] = []

    for table_info in initial_tables:
        components = table_info.split("|")
        if len(components) == 3:
            table_name, key_column_string, model_name_override = components
            key_columns = [x.strip() for x in key_column_string.split(",")]
            real_tables.append(
                ModelConfig(
                    table_name=table_name,
                    key_columns=key_columns,
                    model_name_override=model_name_override,
                )
            )
        elif len(components) == 2:
            table_name, key_column_string = components
            key_columns = [x.strip() for x in key_column_string.split(",")]
            real_tables.append(ModelConfig(table_name=table_name, key_columns=key_columns))
        else:
            real_tables.append(ModelConfig(table_name=table_info, key_columns=[]))

    return real_tables


def config_import_selector(namespace: str, config_lines: list[str]) -> str:
    """Return a string importing necessary namespaces for configuration files."""
    lines_without_property = linq.first_or_default(
        config_lines, lambda x: not x.startswith("builder.Property")
    )
    if "MySql" not in namespace and lines_without_property:
        return "using TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework;"
    return ""


def get_arguments(
    dialect: str,
    image: str | None = None,
    output_root: Path | None = None,
    base_namespace: str | None = None,
    entity_namespace: str | None = None,
    config_namespace: str | None = None,
    initial_tables: list[str] | None = None,
) -> EntityGeneratorConfig:
    """Return an EntityGeneratorConfig."""

    if dialect == "MySql":
        image = image or db_defaults.MYSQL_IMAGE
        output_root = output_root or paths.MYSQL_ENTITY_FRAMEWORK_DIR
    elif dialect == "MariaDb":
        image = image or db_defaults.MARIADB_IMAGE
        output_root = output_root or paths.MARIADB_ENTITY_FRAMEWORK_DIR
    else:
        raise ValueError(f"Unsupported dialect: {dialect}")

    base_namespace = (
        base_namespace or f"TcfOss.DatabaseManager.{dialect}.DatabaseComms.EntityFramework"
    )
    entity_namespace = entity_namespace or f"{base_namespace}.Entities"
    config_namespace = config_namespace or f"{base_namespace}.Configurations"

    tables = _extract_tables(dialect=dialect, initial_tables=initial_tables)

    return EntityGeneratorConfig(
        dialect=dialect,
        image=image,
        output_root=output_root,
        base_namespace=base_namespace,
        entity_namespace=entity_namespace,
        config_namespace=config_namespace,
        context_class="InfoSchemaContext",
        config_import_selector=config_import_selector,
        schema="information_schema",
        tables=tables,
        environment=db_defaults.ENVIRONMENT,
    )


def run_cli() -> None:
    """Run the script from the command line."""

    parser = argparse.ArgumentParser(description="Generate C# entities from database tables.")
    add_version_argument(parser)
    parser.add_argument("dialect", choices=["MySql", "MariaDb"], help="Database dialect.")

    parser.add_argument("--image", help="Docker image for MySQL container.")
    parser.add_argument("--output-root", help="Root directory for output files.")
    parser.add_argument("--base-namespace", help="Base namespace for generated code.")
    parser.add_argument("--entity-namespace", help="Namespace for entity classes.")
    parser.add_argument("--config-namespace", help="Namespace for configuration classes.")
    parser.add_argument(
        "--tables",
        nargs="+",
        help=(
            "List of tables to generate entities for. "
            "Format: TableName|KeyCol1,KeyCol2|ModelNameOverride"
        ),
    )

    args = parser.parse_args()
    config = get_arguments(
        dialect=args.dialect,
        image=args.image,
        output_root=Path(args.output_root) if args.output_root else None,
        base_namespace=args.base_namespace,
        entity_namespace=args.entity_namespace,
        config_namespace=args.config_namespace,
        initial_tables=args.tables,
    )

    main(config)


def main(args: EntityGeneratorConfig) -> None:
    """Run the script."""
    run(args)

    print("Entity generation completed.")


if __name__ == "__main__":
    run_cli()
