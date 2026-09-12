"""Base app manager."""

import abc as _abc
import pathlib as _pathlib
import shutil as _shutil
import sys as _sys
import semver as _semver


class AppManager(_abc.ABC):
    """Base class for managing application installations."""

    def _install(
        self,
        version: str,
        source_dir: _pathlib.Path,
        base_lib_dir: _pathlib.Path,
        bin_dir: _pathlib.Path,
        bin_cmd: str,
        executable_name: str,
        exclude_patterns: list[str] | None = None,
        target_permissions: str | None = None,
        keep_latest: int | None = None,
    ) -> None:
        """Install the application from the source directory to the base library directory."""

        if not source_dir.exists() or not source_dir.is_dir():
            print(
                f"Source directory '{source_dir}' does not exist or is not a directory. Exiting."
            )
            _sys.exit(3)

        print()
        print("Transferring files to installation directory...")
        self._transfer_files(
            source_dir=source_dir,
            target_dir=base_lib_dir / version,
            target_permissions=target_permissions,
            exclude_patterns=exclude_patterns,
        )

        print()
        print("Setting executable permissions for the main executable...")
        self._make_file_executable(base_lib_dir / version / executable_name)

        print()
        print(f"Updating 'current' link to point to version '{version}'...")
        current_link = self._update_current_link(base_lib_dir, version)

        print()
        print(f"Updating executable link in '{bin_dir}'...")
        self._update_executable_link(
            bin_link=bin_dir / bin_cmd,
            target_executable=current_link / executable_name,
        )

        if keep_latest:
            print()
            print(f"Removing old versions, keeping the latest {keep_latest}...")
            self._prune_old_versions(base_lib_dir, keep_latest)

    def _uninstall(
        self, base_lib_dir: _pathlib.Path, bin_dir: _pathlib.Path, bin_cmd: str
    ) -> None:
        """Uninstall all versions of the application."""

        if not base_lib_dir.exists() or not base_lib_dir.is_dir():
            print(f"Installation directory {base_lib_dir} does not exist. Continuing.")
        else:
            print(f"Removing installation directory {base_lib_dir}...")
            _shutil.rmtree(base_lib_dir)

        bin_link = bin_dir / bin_cmd
        if bin_link.exists(follow_symlinks=False):
            print(f"Removing executable link at '{bin_link}'...")
            bin_link.unlink()
        else:
            print(f"No executable link found at '{bin_link}'. Skipping removal.")

    def _rollback(
        self,
        target_version: str,
        base_lib_dir: _pathlib.Path,
        bin_dir: _pathlib.Path,
        bin_cmd: str,
        executable_name: str,
    ) -> None:
        """Rollback to a specific version of the application."""

        available_versions = self._get_version_directories(base_lib_dir)
        if not any(v[0].name == target_version for v in available_versions):
            print(f"Target version '{target_version}' is not installed. Exiting.")
            _sys.exit(5)

        print(f"Rolling back to version '{target_version}'...")
        current_link = self._update_current_link(base_lib_dir, target_version)

        self._update_executable_link(
            bin_link=bin_dir / bin_cmd,
            target_executable=current_link / executable_name,
        )

    def _get_version_directories(
        self, base_lib_dir: _pathlib.Path
    ) -> list[tuple[_pathlib.Path, _semver.Version]]:
        """Get a list of version directories in the base library directory."""

        def is_dir_version(d: _pathlib.Path) -> bool:
            if not d.is_dir():
                return False
            if d.name == "current":
                return False
            try:
                _semver.Version.parse(d.name)
                return True
            except ValueError:
                return False

        if not base_lib_dir.exists() or not base_lib_dir.is_dir():
            return []

        version_dirs = [
            (d, _semver.Version.parse(d.name)) for d in base_lib_dir.iterdir() if is_dir_version(d)
        ]
        version_dirs.sort(key=lambda d: d[1], reverse=True)
        return version_dirs

    def _check_version_regression(
        self, base_lib_dir: _pathlib.Path, version: _semver.Version, force: bool = False
    ) -> None:
        """Check if a specific version is installed."""

        installed_versions = self._get_version_directories(base_lib_dir)
        if not force and installed_versions and installed_versions[0][1] >= version:
            print(
                f"Version {installed_versions[0][1]} is already installed, which is "
                f"newer than or equal to the version being installed ({version}). "
                "Use --force to override."
            )
            _sys.exit(6)

    @_abc.abstractmethod
    def _make_file_executable(self, file_path: _pathlib.Path):
        """Make the specified file executable."""

    @_abc.abstractmethod
    def _transfer_files(
        self,
        source_dir: _pathlib.Path,
        target_dir: _pathlib.Path,
        target_permissions: str | None = None,
        exclude_patterns: list[str] | None = None,
    ):
        """Transfer files from source to target directory."""

    @_abc.abstractmethod
    def _update_current_link(
        self, base_lib_dir: _pathlib.Path, target_version: str
    ) -> _pathlib.Path:
        """Update the 'current' link to point to the target version directory.

        Parameters
        ----------
        base_lib_dir : _pathlib.Path
            The base library directory containing version subdirectories.
        target_version : str
            The target version to which the 'current' link should point.

        Returns
        -------
        _pathlib.Path
            The path to the updated 'current' link.
        """

    @_abc.abstractmethod
    def _update_executable_link(self, bin_link: _pathlib.Path, target_executable: _pathlib.Path):
        """Update the executable link in the bin directory to point to the target executable.

        Parameters
        ----------
        bin_link : _pathlib.Path
            The path to the executable link in the bin directory.
        target_executable : _pathlib.Path
            The path to the target executable file.
        """

    def _prune_old_versions(self, base_lib_dir: _pathlib.Path, keep_latest: int):
        """Prune old versions from the distribution directory.

        Parameters
        ----------
        base_lib_dir : Path
            Base directory under which the application versions are installed.
        keep_latest : int
            Number of latest versions to keep.
        """

        version_dirs = self._get_version_directories(base_lib_dir)

        for old_version_dir, _ in version_dirs[keep_latest:]:
            print(f"Removing old version directory '{old_version_dir}'...")
            _shutil.rmtree(old_version_dir)
