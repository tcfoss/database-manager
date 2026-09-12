"""Run shell commands."""

import os
from pathlib import Path as _Path
import shutil as _shutil
import subprocess as _subproc

from dbman_dev.utilities.rsync_opts import RsyncOpts as _RsyncOpts
from dbman_dev.utilities.platform import IS_UNIX_LIKE as _IS_UNIX_LIKE


def run(cmd: list[str], cwd: _Path | None = None, quiet: bool = False) -> None:
    """Run a shell command."""
    if not quiet:
        cwd_printable = ""
        if cwd:
            cwd_printable = str(cwd)

        if not _IS_UNIX_LIKE:
            print(f"{cwd_printable}> {_subproc.list2cmdline(cmd)}")
        else:
            print(f"{cwd_printable}> {' '.join(cmd)}")

    _subproc.run(cmd, shell=False, cwd=cwd, check=True)


def run_output(cmd: list[str], cwd: _Path | None = None, quiet: bool = False) -> str:
    """Run a shell command and return its output as a string."""

    if not quiet:
        cwd_printable = ""
        if cwd:
            cwd_printable = str(cwd)

        if not _IS_UNIX_LIKE:
            print(f"{cwd_printable}> {_subproc.list2cmdline(cmd)}")
        else:
            print(f"{cwd_printable}> {' '.join(cmd)}")

    result = _subproc.run(cmd, shell=False, cwd=cwd, check=True, capture_output=True, text=True)
    return result.stdout.strip()


def rsync(
    source: _Path, destination: _Path, opts: _RsyncOpts | None = None, quiet: bool = False
) -> None:
    """Run rsync to copy files from source to destination.

    If either source or destination is a directory, a trailing slash will be appended.
    """

    if opts is None:
        opts = _RsyncOpts()

    cmd = ["rsync"] + opts.to_cmd_args()

    source_str = str(source.resolve())
    destination_str = str(destination.resolve())

    # rsync behavior changes based on trailing slashes on paths. Add them
    if source.is_dir():
        source_str += os.sep
    if destination.is_dir():
        destination_str += os.sep

    cmd.extend([source_str, destination_str])

    run(cmd, quiet=quiet)


def verify_rsync_available() -> None:
    """Check if rsync is available on the system."""

    if not _shutil.which("rsync"):
        raise FileNotFoundError("rsync command not found in PATH.")
