"""Generate C# fixtures from selected PostgreSQL system catalog rows."""

import argparse
from pathlib import Path

from dbman_dev.cliversion import add_version_argument
from dbman_dev.infoschema_building.test_data import get_arguments, get_default_projects, run


def run_cli() -> None:
    """Run PostgreSQL catalog fixture generation from the command line."""
    projects = {project.name: project for project in get_default_projects("Postgres")}
    parser = argparse.ArgumentParser(description="Generate C# fixtures from pg_catalog tables.")
    add_version_argument(parser)
    parser.add_argument("--output-path", help="Path to the generated C# file.")
    parser.add_argument("--image", help="PostgreSQL Docker image.")
    parser.add_argument("--test-namespace", help="Namespace for test classes.")
    parser.add_argument("--entity-namespace", help="Namespace for entity classes.")
    parser.add_argument(
        "--projects",
        nargs="+",
        choices=list(projects),
        default=list(projects),
        help="Schema projects to initialize (default: all supported projects).",
    )
    args = parser.parse_args()
    run(
        get_arguments(
            dialect="Postgres",
            image=args.image,
            output_path=Path(args.output_path) if args.output_path else None,
            entity_namespace=args.entity_namespace,
            output_namespace=args.test_namespace,
            projects=[projects[name] for name in args.projects],
        )
    )


if __name__ == "__main__":
    run_cli()
