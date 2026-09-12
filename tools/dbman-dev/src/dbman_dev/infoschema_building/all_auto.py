"""Regenerate all INFORMATION_SCHEMA files."""

from dbman_dev.infoschema_building.entities import (
    get_arguments as eg_get_args,
    main as eg_main
)
from dbman_dev.infoschema_building.test_data import (
    get_arguments as dl_get_args,
    main as dl_main
)

def main() -> None:
    """Regenerate all INFORMATION_SCHEMA files."""
    eg_main(eg_get_args(dialect="MySql"))
    eg_main(eg_get_args(dialect="MariaDb"))

    dl_main(dl_get_args(dialect="MySql"))
    dl_main(dl_get_args(dialect="MariaDb"))
