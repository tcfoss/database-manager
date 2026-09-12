"""Base class for managing Windows application installations."""

import pathlib as _pathlib
import shutil as _shutil
import sys as _sys
import typing as _typing
import dbman_dev.installation.app_manager as _am
from dbman_dev.utilities import shell_exec as _shell_exec


class AppManagerWin(_am.AppManager):
    """Base app manager for Windows systems."""

    def _transfer_files(
        self,
        source_dir: _pathlib.Path,
        target_dir: _pathlib.Path,
        target_permissions: str | None = None,
        exclude_patterns: list[str] | None = None,
    ):
        exclude_callback: _typing.Callable[[str, list[str]], set[str]] | None = None
        if exclude_patterns:
            exclude_callback = _shutil.ignore_patterns(*exclude_patterns)

        if target_dir.exists():
            print(f"Removing existing directory {target_dir}...")
            _shutil.rmtree(target_dir)

        target_dir.parent.mkdir(parents=True, exist_ok=True)
        _shutil.copytree(source_dir, target_dir, ignore=exclude_callback)

    def _create_junction(self, link: _pathlib.Path, target: _pathlib.Path) -> None:
        """Create a junction from link to target."""

        _shell_exec.run(["cmd", "/c", "mklink", "/J", f"{link}", f"{target}"])

    def _make_file_executable(self, file_path: _pathlib.Path):
        """No-op on Windows since .exe files are already executable."""

    def _update_current_link(
        self, base_lib_dir: _pathlib.Path, target_version: str
    ) -> _pathlib.Path:
        """Update the 'current' junction to point to the target version on Windows systems.

        Parameters
        ----------
        base_lib_dir : Path
            Base directory under which the application versions are installed.
        target_version : str
            The version string to which the 'current' link should point.

        Returns
        -------
        Path
            The updated 'current' junction path.
        """

        current = base_lib_dir.parent / "current"
        target = base_lib_dir / target_version

        if not target.is_dir():
            print(
                f"Version {target_version} not found in {base_lib_dir}."
                " Cannot update 'current' link."
            )
            _sys.exit(8)

        if current.exists(follow_symlinks=False):
            current.unlink()

        self._create_junction(current, target)
        return current

    def _update_executable_link(self, bin_link: _pathlib.Path, target_executable: _pathlib.Path):
        """Generate a .cmd file in bin_link that points to target_executable."""

        bin_link.parent.mkdir(parents=True, exist_ok=True)

        executable_rel = target_executable.relative_to(bin_link.parent, walk_up=True)

        executable_rel_posix = executable_rel.as_posix()
        executable_rel_str = executable_rel_posix.replace("/", "\\")

        exec_text = f'@"%~dp0{executable_rel_str}" %*\r\n'
        with open(bin_link, "w", encoding="utf-8", newline="") as link_file:
            link_file.write(exec_text)

        sh_wrapper_text = (
            "#!/bin/bash\n"
            """CURR_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"\n"""
            f"""exec "$CURR_DIR/{executable_rel_posix}" "$@"\n"""
        )

        sh_link = bin_link.with_suffix("")
        with open(sh_link, "w", encoding="utf-8", newline="") as link_file:
            link_file.write(sh_wrapper_text)
        sh_link.chmod(0o755)
