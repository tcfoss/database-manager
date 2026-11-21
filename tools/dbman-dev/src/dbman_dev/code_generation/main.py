"""Entry point for regenerating catalog-table models and test data."""

import argparse

from dbman_dev.cliversion import add_version_argument
from dbman_dev.code_generation import generate_models_and_data, projects
from dbman_dev.code_generation.dialect import SqlDialect


def run_cli() -> None:
    """Run the script from the command line."""

    parser = argparse.ArgumentParser(description="Generate C# entities and test data.")
    add_version_argument(parser)

    parser.add_argument("dialect", choices=[e.value for e in SqlDialect], help="Database dialect.")
    parser.add_argument(
        "schemas", nargs="*", help="Fixture schemas to extract (default: all for the dialect)."
    )

    parser.add_argument(
        "--skip-models", action="store_true", help="Skip generating entities and configurations"
    )
    parser.add_argument("--skip-data", action="store_true", help="Skip generating test data")

    args = parser.parse_intermixed_args()
    if args.skip_models and args.skip_data:
        parser.error("--skip-models and --skip-data cannot be used together")

    try:
        config = projects.get_config(SqlDialect.parse(args.dialect), args.schemas)
    except ValueError as error:
        parser.error(str(error))

    if args.skip_models:
        config.project_config.entity_dir = None
        config.project_config.config_dir = None
    if args.skip_data:
        config.project_config.data_file = None

    generate_models_and_data.run(config)


if __name__ == "__main__":
    run_cli()
