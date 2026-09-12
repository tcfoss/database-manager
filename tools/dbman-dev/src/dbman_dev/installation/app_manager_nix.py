"""Base class for managing linux/macOS application installations."""

import pathlib as _pathlib
import sys as _sys
import dbman_dev.installation.app_manager as _am

from dbman_dev.utilities.shell_exec import rsync as _rsync
from dbman_dev.utilities.rsync_opts import RsyncOpts as _RsyncOpts


class AppManagerNix(_am.AppManager):
    """Base app manager for Linux and macOS systems."""

    def _transfer_files(
        self,
        source_dir: _pathlib.Path,
        target_dir: _pathlib.Path,
        target_permissions: str | None = None,
        exclude_patterns: list[str] | None = None,
    ):
        """Transfer files from the source directory to the target directory."""

        if target_permissions:
            opts = _RsyncOpts(
                archive=True,
                delete=True,
                chmod=target_permissions,
                exclude_patterns=exclude_patterns,
            )
        else:
            opts = _RsyncOpts(archive=True, delete=True, exclude_patterns=exclude_patterns)

        _rsync(source_dir, target_dir, opts=opts)

    def _make_file_executable(self, file_path: _pathlib.Path):
        """Make the specified file executable."""
        if not file_path.exists():
            print(f"File '{file_path}' does not exist. Cannot make executable.")
            _sys.exit(4)

        try:
            current_permissions = file_path.stat().st_mode
            new_permissions = current_permissions | 0o111
            file_path.chmod(new_permissions)
            print(f"Set executable permissions for '{file_path}'.")
        except PermissionError as e:
            print(f"User lacks permissions to chmod '{file_path}': {e}")
            print("You may need to manually set the executable permissions for this file.")

    def _update_current_link(
        self, base_lib_dir: _pathlib.Path, target_version: str
    ) -> _pathlib.Path:

        current_link = base_lib_dir / "current"
        target_dir = base_lib_dir / target_version

        if not target_dir.exists() or not target_dir.is_dir():
            print(f"Target version directory '{target_dir}' does not exist. Exiting.")
            _sys.exit(4)

        if (
            current_link.exists(follow_symlinks=True)
            and current_link.resolve() == target_dir.resolve()
        ):
            print(f"'current' link already points to '{target_dir}'. No update needed.")
            return current_link

        if current_link.exists(follow_symlinks=False) and not current_link.is_symlink():
            print("'current' exists and is not a symlink. Not replacing...")
            _sys.exit(7)

        temp_link = base_lib_dir / ".current.tmp"
        if temp_link.exists(follow_symlinks=False):
            temp_link.unlink()

        temp_link.symlink_to(target_dir.name)
        temp_link.replace(current_link)
        return current_link

    def _update_executable_link(self, bin_link: _pathlib.Path, target_executable: _pathlib.Path):
        """Generate a .cmd file in bin_link that points to target_executable."""
        if (
            bin_link.exists(follow_symlinks=True)
            and bin_link.resolve() == target_executable.resolve()
        ):
            print("Executable link is already up to date. No action needed.")
            return

        if bin_link.exists(follow_symlinks=False):
            print(
                f"Removing existing reference at '{bin_link}', "
                f"pointing to '{bin_link.resolve()}'..."
            )
            bin_link.unlink()
        print(f"Creating new executable link at '{bin_link}' pointing to '{target_executable}'...")
        bin_link.symlink_to(target_executable)
