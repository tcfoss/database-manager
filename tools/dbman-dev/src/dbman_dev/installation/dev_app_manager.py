"""Base manager for application installation."""

import shutil as _shutil
import sys as _sys
from pathlib import Path as _Path
from typing import ClassVar as _ClassVar, cast as _cast
import semver as _semver
from dbman_dev.paths import REPO_ROOT as _REPO_ROOT

from dbman_dev.utilities.shell_exec import run as _run
from dbman_dev.utilities.git import get_latest_tag_version as _get_version
from dbman_dev.installation import app_manager as _am


class DevAppManager(_am.AppManager):
    """Base class for managing application installation."""

    _PROJ_NAME: _ClassVar[str] = "TcfOss.DatabaseManager.App"
    _PROJ_PATH: _ClassVar[_Path] = _REPO_ROOT / "src" / _PROJ_NAME / f"{_PROJ_NAME}.csproj"
    _DIST_PATH: _ClassVar[_Path] = _REPO_ROOT / "dist" / _PROJ_NAME

    _LIB_NAME: _ClassVar[str] = "tcf-database-manager"
    _BIN_COMMAND: _ClassVar[str] = "dbman"
    _EXE_NAME: _ClassVar[str] = "TcfOss.DatabaseManager.App"

    _ARCHITECTURE: _ClassVar[str | None] = None

    def __init__(
        self,
        base_lib_dir: _Path,
        bin_dir: _Path,
        dist_executable: str,
        target_permissions: str | None,
    ):
        """Initialize the AppManager."""
        self.base_lib_dir: _Path = base_lib_dir
        self.bin_dir: _Path = bin_dir
        self.executable_name = dist_executable
        self.target_permissions: str | None = target_permissions

        self.base_lib_dir.mkdir(parents=True, exist_ok=True)
        self.bin_dir.mkdir(parents=True, exist_ok=True)

    def install(
        self,
        framework_dependent: bool = False,
        keep_latest: int | None = None,
        force: bool = False,
    ):
        """Install the application.

        Parameters
        ----------
        framework_dependent : bool, optional
            Whether to install as a framework-dependent deployment (Default `False`).
        keep_latest : int | None, optional
            Number of latest versions to keep (Default `None`).
        force : bool, optional
            Whether to force installation (Default `False`).
        """

        version = _get_version()
        self._check_version_regression(
            base_lib_dir=self.base_lib_dir,
            version=version,
            force=force,
        )

        print()
        print(f"Publishing application '{DevAppManager._PROJ_NAME}'...")
        self._publish_app(framework_dependent=framework_dependent)
        print(f"{DevAppManager._PROJ_NAME} published successfully.")

        self._install(
            version=str(version),
            source_dir=DevAppManager._DIST_PATH,
            base_lib_dir=self.base_lib_dir,
            bin_dir=self.bin_dir,
            bin_cmd=DevAppManager._BIN_COMMAND,
            executable_name=self.executable_name,
            target_permissions=self.target_permissions,
            keep_latest=keep_latest,
        )

    def uninstall(self):
        """Uninstall the application."""

        self._uninstall(self.base_lib_dir, self.bin_dir, DevAppManager._BIN_COMMAND)

    def rollback(self, target_version: str):
        """Rollback to a specific version of the application."""

        self._rollback(
            target_version,
            self.base_lib_dir,
            self.bin_dir,
            DevAppManager._BIN_COMMAND,
            self.executable_name,
        )

    def _publish_app(self, framework_dependent: bool = False):
        """Publish the application to the dist directory.

        Parameters
        ----------
        framework_dependent : bool, optional
            Whether to publish as a framework-dependent deployment (Default `False`).
        """

        if not framework_dependent and not self._ARCHITECTURE:
            raise RuntimeError("Architecture must be defined for self-contained deployments.")

        if DevAppManager._DIST_PATH.exists():
            print(f"Cleaning previous publish results at '{DevAppManager._DIST_PATH}'...")
            _shutil.rmtree(DevAppManager._DIST_PATH)

        if DevAppManager._DIST_PATH.exists():
            print(
                "Failed to clean previous publish results "
                f"at '{DevAppManager._DIST_PATH}'. Exiting."
            )
            _sys.exit(2)

        print("Publishing application...")
        cmd = ["dotnet", "publish", str(DevAppManager._PROJ_PATH), "-c", "Release"]

        if not framework_dependent:
            cmd.extend(["-r", _cast(str, self._ARCHITECTURE), "--self-contained", "true"])

        cmd.extend(["-o", str(DevAppManager._DIST_PATH)])

        _run(cmd)

        if not DevAppManager._DIST_PATH.exists():
            print(f"Failed to publish application to {DevAppManager._DIST_PATH}. Exiting.")
            _sys.exit(3)
