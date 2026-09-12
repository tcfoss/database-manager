"""Print stuff for debugging purposes."""

from importlib.metadata import version
from dbman_dev import paths


def main():
    """Run the debug help script."""
    print("Version:", version("dbman-dev"))
    print("Repository root path:", paths.REPO_ROOT)


if __name__ == "__main__":
    main()
