"""App installation manager for Linux systems."""

from pathlib import Path as _Path
from typing import ClassVar as _ClassVar
from dbman_dev.installation.dev_app_manager import DevAppManager as _DevAppManager
from dbman_dev.installation.app_manager_nix import AppManagerNix as _AppManagerNix
from dbman_dev.utilities.shell_exec import rsync as _rsync
from dbman_dev.utilities.rsync_opts import RsyncOpts as _RsyncOpts


class LinuxDevAppManager(_DevAppManager, _AppManagerNix):
    """App manager for Linux systems."""

    _ARCHITECTURE: _ClassVar[str | None] = "linux-x64"

    def __init__(self, system_wide: bool = False):
        """Initialize the LinuxDevAppManager."""

        target_permissions: str | None
        if system_wide:
            base_lib_dir = _Path("/usr/local/lib") / _DevAppManager._LIB_NAME
            bin_dir = _Path("/usr/local/bin")
            target_permissions = "Du=rwx,Dgo=rx,Fu=rw,Fgo=r"
        else:
            base_lib_dir = _Path.home() / ".local" / "lib" / _DevAppManager._LIB_NAME
            bin_dir = _Path.home() / ".local" / "bin"
            target_permissions = None

        super().__init__(
            base_lib_dir=base_lib_dir,
            bin_dir=bin_dir,
            dist_executable=_DevAppManager._EXE_NAME,
            target_permissions=target_permissions,
        )
