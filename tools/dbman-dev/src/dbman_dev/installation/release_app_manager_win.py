"""App manager for built-release application installation on Windows systems."""

import os as _os
from pathlib import Path as _Path
from typing import ClassVar as _ClassVar
import semver as _semver

from dbman_dev.utilities.shell_exec import run_output as _run_output
from dbman_dev.installation.release_app_manager import ReleaseAppManager as _ReleaseAppManager
from dbman_dev.installation.app_manager_win import AppManagerWin as _AppManagerWin

class WindowsReleaseAppManager(_ReleaseAppManager, _AppManagerWin):
    """App manager for built-release application installation on Windows systems."""

    _ARCHITECTURE: _ClassVar[str | None] = "win-x64"
    _BIN_COMMAND: _ClassVar[str] = "dbman.cmd"

    def __init__(self, system_wide: bool = False):
        """Initialize the WindowsReleaseAppManager."""

        if system_wide:
            base_dir = _Path(_os.environ["ProgramFiles"]) / _ReleaseAppManager._LIB_NAME
            bin_dir = base_dir / "bin"
        else:
            base_dir = _Path(_os.environ["LOCALAPPDATA"]) / _ReleaseAppManager._LIB_NAME
            bin_dir = base_dir / "bin"

        super().__init__(
            base_lib_dir=base_dir / "versions",
            bin_dir=bin_dir,
            dist_executable=_ReleaseAppManager._EXE_NAME + ".exe",
            target_permissions=None,
        )

    def _get_version(self, release_dir: _Path):
        """Return the version."""
        print("Determining release version...")
        executable = (release_dir / self.executable_name).resolve()
        version_str = _run_output([f"{executable}", "--version"]).strip()
        print(f"Found version string: '{version_str}'")
        return _semver.VersionInfo.parse(version_str)
