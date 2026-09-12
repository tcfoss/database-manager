"""App installation manager for Windows."""

import os as _os
from pathlib import Path as _Path
from typing import ClassVar as _ClassVar
from dbman_dev.installation.dev_app_manager import DevAppManager as _DevAppManager
from dbman_dev.installation.app_manager_win import AppManagerWin as _AppManagerWin


class WindowsDevAppManager(_DevAppManager, _AppManagerWin):
    """App manager for Windows systems."""

    _ARCHITECTURE: _ClassVar[str | None] = "win-x64"
    _BIN_COMMAND: _ClassVar[str] = "dbman.cmd"

    def __init__(self, system_wide: bool = False):
        """Initialize the WindowsAppManager."""

        if system_wide:
            base_dir = _Path(_os.environ["ProgramFiles"]) / _DevAppManager._LIB_NAME
            bin_dir = base_dir / "bin"
        else:
            base_dir = _Path(_os.environ["LOCALAPPDATA"]) / _DevAppManager._LIB_NAME
            bin_dir = base_dir / "bin"

        super().__init__(
            base_lib_dir=base_dir / "versions",
            bin_dir=bin_dir,
            dist_executable=_DevAppManager._EXE_NAME + ".exe",
            target_permissions=None,
        )
