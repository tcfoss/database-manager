"""Common commands for packing nuget packages."""

from pathlib import Path as _Path
import semver as _sv
from dbman_dev.utilities.shell_exec import run as _run


def pack(
    project_path: _Path,
    output: _Path,
    version: _sv.Version,
    package_id: str,
    release: bool = True,
    quiet: bool = False,
) -> None:
    """Run dotnet pack.

    Parameters
    ----------
    project_path : Path
        The path to the .csproj or .sln or .slnx file defining what to pack.
    output : Path
        The output directory for the packed nuget package.
    version : semver.Version
        The version to pack.
    package_id : str
        The package ID to use.
    release : bool
        Whether to build in Release configuration. Defaults to True.
    quiet : bool
        Whether to omit printing the command being run. Defaults to False.
    """

    cmd = ["dotnet", "pack", str(project_path)]

    if release:
        cmd.extend(["-c", "Release"])  # Build in Release configuration
    else:
        cmd.extend(["-c", "Debug"])  # Build in Debug configuration

    cmd.extend(["-o", str(output)])

    if version:
        cmd.append(f"/p:PackageVersion={version}")
    if package_id:
        cmd.append(f"/p:PackageId={package_id}")

    _run(cmd, quiet=quiet)


def build(project_path: _Path, release: bool = True, quiet: bool = False) -> None:
    """Build the specified project.

    Parameters
    ----------
    project_path : Path
        The path to the .csproj or .sln or .slnx file defining what to build.
    release : bool
        Whether to build in Release configuration. Defaults to True.
    quiet : bool
        Whether to omit printing the command being run. Defaults to False.
    """

    cmd = [
        "dotnet",
        "build",
        str(project_path),
    ]

    if release:
        cmd.extend(["-c", "Release"])  # Build in Release configuration
    else:
        cmd.extend(["-c", "Debug"])  # Build in Debug configuration

    _run(cmd, quiet=quiet)
