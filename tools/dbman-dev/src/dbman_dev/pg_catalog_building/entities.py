"""Generate C# entity/configuration models for selected PostgreSQL catalogs."""

import argparse
from pathlib import Path

from dbman_dev.cliversion import add_version_argument
from dbman_dev.infoschema_building.entities import get_arguments, main


def run_cli() -> None:
    """Run PostgreSQL catalog entity generation from the command line."""
    parser = argparse.ArgumentParser(description="Generate C# entities from pg_catalog tables.")
    add_version_argument(parser)
    parser.add_argument("--image", help="PostgreSQL Docker image.")
    parser.add_argument("--output-root", help="Root directory for generated files.")
    parser.add_argument("--base-namespace", help="Base namespace for generated code.")
    parser.add_argument("--entity-namespace", help="Namespace for entity classes.")
    parser.add_argument("--config-namespace", help="Namespace for configuration classes.")
    parser.add_argument(
        "--tables",
        nargs="+",
        help="List of tables to generate. Format: TableName|KeyCol1,KeyCol2|ModelNameOverride",
    )
    args = parser.parse_args()
    main(
        get_arguments(
            dialect="Postgres",
            image=args.image,
            output_root=Path(args.output_root) if args.output_root else None,
            base_namespace=args.base_namespace,
            entity_namespace=args.entity_namespace,
            config_namespace=args.config_namespace,
            initial_tables=args.tables,
        )
    )


if __name__ == "__main__":
    run_cli()
