"""Get C# entities to simulate MySQL information_schema records for test database."""

import argparse
from pathlib import Path
from dbman_dev.csharp_code_generation.model_config import ModelConfig
from dbman_dev.csharp_code_generation.model_data_loader import (
    run,
    ModelDataLoaderConfig,
    ProjectConfig,
)
from dbman_dev.cliversion import add_version_argument
from dbman_dev.db import defaults as db_defaults, info_schema
from dbman_dev.csharp_code_generation import paths


def get_default_projects() -> list[ProjectConfig]:
    """Return fresh configurations for the supported schema projects."""
    return [
        ProjectConfig(
            name="Library",
            source_path=paths.MYSQL_TEST_PROJECTS_DIR / "Library" / "Initial",
            schemas=["library_catalog", "library_identity", "library_activity"],
            initialization_scripts=["initialize_library.sh"],
        ),
        ProjectConfig(
            name="ExtraTest",
            source_path=paths.MYSQL_TEST_PROJECTS_DIR / "ExtraTest" / "Initial",
            schemas=["extra_test"],
            initialization_scripts=["initialize_db.sh"],
        ),
    ]


def get_arguments(
    dialect: str,
    image: str | None = None,
    output_path: Path | None = None,
    projects: list[ProjectConfig] | None = None,
    entity_namespace: str | None = None,
    output_namespace: str | None = None,
    tables: list[ModelConfig] | None = None,
) -> ModelDataLoaderConfig:
    """Return arguments for the model data loader."""

    if dialect == "MySql":
        image = image or db_defaults.MYSQL_IMAGE
        output_path = output_path or paths.MYSQL_TEST_DATA
        tables = tables or list(info_schema.MYSQL_CONFIGS.values())
        entity_namespace = (
            entity_namespace
            or "TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework.Entities"
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

    return ModelDataLoaderConfig(
        dialect=dialect,
        image=image,
        output_path=output_path,
        projects=get_default_projects() if projects is None else projects,
        entity_namespace=entity_namespace,
        output_namespace=output_namespace,
        tables=tables,
    )


def run_cli() -> None:
    """Run the script from command line."""

    project_configs = {project.name: project for project in get_default_projects()}
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
        "--projects",
        nargs="+",
        choices=list(project_configs),
        default=list(project_configs),
        help="Schema projects to initialize (default: all supported projects).",
    )

    args = parser.parse_args()
    if len(args.projects) != len(set(args.projects)):
        parser.error("--projects must not contain duplicate project names")

    config = get_arguments(
        dialect=args.dialect,
        image=args.image,
        output_path=Path(args.output_path) if args.output_path else None,
        entity_namespace=args.entity_namespace,
        output_namespace=args.test_namespace,
        projects=[project_configs[name] for name in args.projects],
    )

    run(config)


def main(args: ModelDataLoaderConfig) -> None:
    """Run the script."""

    run(args)


if __name__ == "__main__":
    run_cli()
