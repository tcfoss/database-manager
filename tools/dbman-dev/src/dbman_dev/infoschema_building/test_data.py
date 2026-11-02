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
from dbman_dev.containers import db_container
from dbman_dev.containers.db_container import PostgresContainer
from dbman_dev.db.get_show_fields_results import get_show_fields_results
from dbman_dev.db.postgres_catalog import POSTGRES_CONFIGS
from dbman_dev.db.postgres_fields import get_postgres_entity_models


def get_default_projects(dialect: str = "MySql") -> list[ProjectConfig]:
    """Return fresh configurations for the supported schema projects."""
    if dialect == "Postgres":
        return [
            ProjectConfig(
                name="Library",
                source_path=paths.POSTGRES_TEST_PROJECTS_DIR / "Library" / "Initial",
                schemas=["library_catalog", "library_identity", "library_activity"],
                initialization_scripts=["initialize_library.sh"],
            )
        ]
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
    elif dialect == "Postgres":
        image = image or db_defaults.POSTGRES_IMAGE
        output_path = output_path or paths.POSTGRES_TEST_DATA
        tables = tables or list(POSTGRES_CONFIGS.values())
        entity_namespace = (
            entity_namespace
            or "TcfOss.DatabaseManager.Postgres.DatabaseComms.EntityFramework.Entities"
        )
        output_namespace = (
            output_namespace or "TcfOss.DatabaseManager.Postgres.Tests.DatabaseComms"
        )
    else:
        raise ValueError(f"Unsupported dialect: {dialect}")

    return ModelDataLoaderConfig(
        dialect=dialect,
        image=image,
        output_path=output_path,
        projects=get_default_projects(dialect) if projects is None else projects,
        entity_namespace=entity_namespace,
        output_namespace=output_namespace,
        tables=tables,
        environment=(
            db_defaults.POSTGRES_ENVIRONMENT if dialect == "Postgres" else db_defaults.ENVIRONMENT
        ),
        container_factory=(
            PostgresContainer if dialect == "Postgres" else db_container.MySqlContainer
        ),
        column_loader=(
            get_postgres_entity_models if dialect == "Postgres" else get_show_fields_results
        ),
        container_port=5433 if dialect == "Postgres" else 3309,
        metadata_schema="pg_catalog" if dialect == "Postgres" else "information_schema",
    )


def run_cli() -> None:
    """Run the script from command line."""

    project_choices = ["Library", "ExtraTest"]
    parser = argparse.ArgumentParser(description="Generate C# test data from database metadata.")
    add_version_argument(parser)
    parser.add_argument(
        "dialect", choices=["MySql", "MariaDb", "Postgres"], help="Database dialect."
    )
    parser.add_argument("--output-path", help="Path to the output C# file.")
    parser.add_argument("--image", help="Docker image for MySQL.")
    parser.add_argument("--test-namespace", help="Namespace for test classes.")
    parser.add_argument("--entity-namespace", help="Namespace for entity classes.")
    parser.add_argument(
        "--projects",
        nargs="+",
        choices=project_choices,
        default=None,
        help="Schema projects to initialize (default: all supported projects for the dialect).",
    )

    args = parser.parse_args()
    project_configs = {project.name: project for project in get_default_projects(args.dialect)}
    selected_projects = args.projects or list(project_configs)
    unknown_projects = set(selected_projects) - project_configs.keys()
    if unknown_projects:
        parser.error(
            f"projects are unsupported for {args.dialect}: {', '.join(sorted(unknown_projects))}"
        )
    if len(selected_projects) != len(set(selected_projects)):
        parser.error("--projects must not contain duplicate project names")

    config = get_arguments(
        dialect=args.dialect,
        image=args.image,
        output_path=Path(args.output_path) if args.output_path else None,
        entity_namespace=args.entity_namespace,
        output_namespace=args.test_namespace,
        projects=[project_configs[name] for name in selected_projects],
    )

    run(config)


def main(args: ModelDataLoaderConfig) -> None:
    """Run the script."""

    run(args)


if __name__ == "__main__":
    run_cli()
