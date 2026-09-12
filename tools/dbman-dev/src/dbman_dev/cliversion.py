"""Add the version option to argparse."""

import argparse
from importlib.metadata import version


def add_version_argument(parser: argparse.ArgumentParser):
    """Add a --version argument to the given ArgumentParser."""
    parser.add_argument(
        "--version",
        action="version",
        version=f"%(prog)s {version('dbman-dev')}",
        help="Show the version of the tool and exit.",
    )
