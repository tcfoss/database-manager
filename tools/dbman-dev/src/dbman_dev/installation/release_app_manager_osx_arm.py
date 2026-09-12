"""App manager for built-release application installation on Mac systems."""

import pathlib as _pathlib
from typing import ClassVar as _ClassVar
import semver as _semver

from dbman_dev.utilities.shell_exec import run_output as _run_output
from dbman_dev.installation.release_app_manager import ReleaseAppManager as _ReleaseAppManager
from dbman_dev.installation.app_manager_nix import AppManagerNix as _AppManagerNix

class MacReleaseAppManager(_ReleaseAppManager, _AppManagerNix):
    """App manager for built-release application installation on Mac systems."""

    _ARCHITECTURE: _ClassVar[str | None] = "osx-arm"

    def __init__(self, system_wide: bool = False):
        """Initialize the MacReleaseAppManager."""

        target_permissions: str | None
        if system_wide:
            base_lib_dir = _pathlib.Path("/usr/local/lib") / _ReleaseAppManager._LIB_NAME
            bin_dir = _pathlib.Path("/usr/local/bin")
            target_permissions = "Du=rwx,Dgo=rx,Fu=rw,Fgo=r"
        else:
            base_lib_dir = _pathlib.Path.home() / ".local" / "lib" / _ReleaseAppManager._LIB_NAME
            bin_dir = _pathlib.Path.home() / ".local" / "bin"
            target_permissions = None

        super().__init__(
            base_lib_dir=base_lib_dir,
            bin_dir=bin_dir,
            dist_executable=_ReleaseAppManager._EXE_NAME,
            target_permissions=target_permissions,
        )


    def _get_version(self, release_dir):
        """Return the version."""
        print("Determining release version...")
        version_str = _run_output(
            [f"./{self.executable_name}", "--version"], cwd=release_dir.resolve()
        ).strip()
        print(f"Found version string: '{version_str}'")
        return _semver.VersionInfo.parse(version_str)
