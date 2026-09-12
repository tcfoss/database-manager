"""Extract the installer version from the pyproject.toml file."""

import tomllib as _tomllib
import pathlib as _pathlib


def read_installer_version() -> str:
    try:
        pyproject = _pathlib.Path(__file__).resolve().parents[3] / "pyproject.toml"
        with pyproject.open("rb") as _f:
            pp_version = _tomllib.load(_f)["tool"]["dbman-dev"]["installer-version"]

        with open(_pathlib.Path(__file__).resolve().parent / "_build_meta.py", "w") as f:
            f.write(f"__installer_version__ = '{pp_version}'\n")

        return pp_version

    except (ImportError, FileNotFoundError):
        from dbman_dev.installation._build_meta import __installer_version__ as bm_version

        return bm_version

if __name__ == "__main__":
    print(read_installer_version())
