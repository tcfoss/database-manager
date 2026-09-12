"""Get C# entities to simulate MySQL information_schema records for test database."""

import argparse
from pathlib import Path
from dbman_dev.csharp_code_generation.model_config import ModelConfig
from dbman_dev.csharp_code_generation.model_data_loader import run, ModelDataLoaderConfig
from dbman_dev.cliversion import add_version_argument
from dbman_dev.db import defaults as db_defaults, info_schema
from dbman_dev.csharp_code_generation import paths


def get_arguments(
    dialect: str,
    image: str | None = None,
    output_path: Path | None = None,
    source_path: Path | None = None,
    entity_namespace: str | None = None,
    output_namespace: str | None = None,
    tables: list[ModelConfig] | None = None,
    initialization_scripts: list[str] | None = None,
    schemas: list[str] | None = None,
) -> ModelDataLoaderConfig:
    """Return arguments for the model data loader."""

    if dialect == "MySql":
        image = image or db_defaults.MYSQL_IMAGE
        output_path = output_path or paths.MYSQL_TEST_DATA
        tables = tables or list(info_schema.MYSQL_CONFIGS.values())
        entity_namespace = (
            entity_namespace or "TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework.Entities"
        )
        output_namespace = output_namespace or "TcfOss.DatabaseManager.MySql.Tests.DatabaseComms"
    elif dialect == "MariaDb":
        image = image or db_defaults.MARIADB_IMAGE
        output_path = output_path or paths.MARIADB_TEST_DATA
        tables = tables or list(info_schema.MARIADB_CONFIGS.values())
        entity_namespace = (
            entity_namespace
            or "TcfOss.DatabaseManager.MariaDb.DatabaseComms.EntityFramework.Entities"
        )
        output_namespace = output_namespace or "TcfOss.DatabaseManager.MariaDb.Tests.DatabaseComms"
    else:
        raise ValueError(f"Unsupported dialect: {dialect}")

    schemas = schemas or db_defaults.SCHEMAS

    initialization_scripts = initialization_scripts or ["initialize_library.sh"]
    source_path = source_path or paths.MYSQL_TEST_SCHEMA_DIR

    return ModelDataLoaderConfig(
        dialect=dialect,
        image=image,
        output_path=output_path,
        source_path=source_path,
        entity_namespace=entity_namespace,
        output_namespace=output_namespace,
        tables=tables,
        initialization_scripts=initialization_scripts,
        schemas=schemas,
    )


def run_cli() -> None:
    """Run the script from command line."""

    parser = argparse.ArgumentParser(
        description="Generate C# entities for MySQL information_schema."
    )
    add_version_argument(parser)
    parser.add_argument(
        "dialect", choices=["MySql", "MariaDb"], help="Database dialect, e.g., MySql or MariaDb."
    )
    parser.add_argument("--output-path", help="Path to the output C# file.")
    parser.add_argument("--image", help="Docker image for MySQL.")
    parser.add_argument("--test-namespace", help="Namespace for test classes.")
    parser.add_argument("--entity-namespace", help="Namespace for entity classes.")
    parser.add_argument(
        "--schemas",
        nargs="+",
        default=["library_catalog", "library_identity", "library_activity"],
        help="List of schemas to include.",
    )

    args = parser.parse_args()

    config = get_arguments(
        dialect=args.dialect,
        image=args.image,
        output_path=Path(args.output_path) if args.output_path else None,
        entity_namespace=args.entity_namespace,
        output_namespace=args.test_namespace,
        schemas=args.schemas,
    )

    run(config)


def main(args: ModelDataLoaderConfig) -> None:
    """Run the script."""

    run(args)


if __name__ == "__main__":
    run_cli()
