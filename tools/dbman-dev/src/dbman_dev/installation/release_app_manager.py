"""Base manager for built-release application installation."""

import abc as _abc
import sys as _sys
import pathlib as _pathlib
from typing import ClassVar as _ClassVar
import semver as _semver
from dbman_dev.installation import app_manager as _am
from dbman_dev.installation.get_installer_version import (
    read_installer_version as _read_installer_version,
)


_DBMAN_INSTALLER_BASE = "dbman-installer"
_DBMAN_INSTALLER_VERSION = _read_installer_version()


class ReleaseAppManager(_am.AppManager):
    """Base manager for application installation from built release packages."""

    _PROJ_NAME: _ClassVar[str] = "TcfOss.DatabaseManager.App"
    _LIB_NAME: _ClassVar[str] = "tcf-database-manager"
    _BIN_COMMAND: _ClassVar[str] = "dbman"
    _EXE_NAME: _ClassVar[str] = "dbman"

    _ARCHITECTURE: _ClassVar[str | None] = None

    def __init__(
        self,
        base_lib_dir: _pathlib.Path,
        bin_dir: _pathlib.Path,
        dist_executable: str,
        target_permissions: str | None,
    ):
        """Initialize the ReleaseAppManager."""
        self.base_lib_dir: _pathlib.Path = base_lib_dir
        self.bin_dir: _pathlib.Path = bin_dir
        self.executable_name = dist_executable
        self.target_permissions: str | None = target_permissions

        self.base_lib_dir.mkdir(parents=True, exist_ok=True)
        self.bin_dir.mkdir(parents=True, exist_ok=True)

    def release_install(
        self,
        release_dir: _pathlib.Path | None = None,
        force: bool = False,
        keep_latest: int | None = None,
    ):
        """Install the application from the latest release build."""

        if not release_dir:
            release_dir = self._find_release_dir()

        if not release_dir.exists() or not release_dir.is_dir():
            print(
                f"Release directory '{release_dir}' does not exist or is not a directory. "
                "Exiting."
            )
            _sys.exit(5)
        if not (release_dir / self.executable_name).exists():
            print(
                f"Executable '{self.executable_name}' not found in release directory "
                f"'{release_dir}'. Exiting."
            )
            _sys.exit(6)

        version = self._get_version(release_dir)

        self._check_version_regression(
            base_lib_dir=self.base_lib_dir,
            version=version,
            force=force,
        )

        installer_name = _DBMAN_INSTALLER_BASE
        if self._ARCHITECTURE:
            installer_name += f"-{self._ARCHITECTURE}"
        installer_name += f"-v{_DBMAN_INSTALLER_VERSION}"

        self._install(
            version=str(version),
            source_dir=release_dir,
            base_lib_dir=self.base_lib_dir,
            bin_dir=self.bin_dir,
            bin_cmd=ReleaseAppManager._BIN_COMMAND,
            executable_name=self.executable_name,
            keep_latest=keep_latest,
            target_permissions=self.target_permissions,
            exclude_patterns=[installer_name],
        )

    def _find_release_dir(self) -> _pathlib.Path:
        """Find the release build directory."""

        start_dir = _pathlib.Path.cwd()
        for item in start_dir.iterdir():
            if item.name == self.executable_name:
                print(f"Found files to install in directory '{start_dir.resolve()}'.")
                return start_dir

            if item.is_dir():
                for sub_item in item.iterdir():
                    if sub_item.name == self.executable_name:
                        print(f"Found files to install in directory '{item.resolve()}'.")
                        return item

        print("Could not find executable in current directory or its immediate subdirectories.")
        _sys.exit(10)

    def uninstall(self):
        """Uninstall the application."""

        self._uninstall(self.base_lib_dir, self.bin_dir, ReleaseAppManager._BIN_COMMAND)

    def rollback(self, target_version: str):
        """Rollback to a specific version of the application."""

        self._rollback(
            target_version,
            self.base_lib_dir,
            self.bin_dir,
            ReleaseAppManager._BIN_COMMAND,
            self.executable_name,
        )

    @_abc.abstractmethod
    def _get_version(self, release_dir: _pathlib.Path) -> _semver.Version:
        """Return the version."""
