"""Manage installations from application release binaries."""

import argparse as _argparse
import os as _os
import pathlib as _pathlib
import subprocess as _subprocess
import sys

import semver as _semver

import dbman_dev.utilities.platform as _platform
import dbman_dev.installation.release_app_manager_linux as _ml
import dbman_dev.installation.release_app_manager_win as _mw
import dbman_dev.installation.release_app_manager_osx_arm as _mm
from dbman_dev.installation.release_app_manager import _DBMAN_INSTALLER_VERSION
from dbman_dev.installation.dist_client import DistClient as _DistClient
from dbman_dev.installation.get_installer_version import (
    read_installer_version as _read_installer_version,
)


def _get_current_architecture() -> str:
    """Return the architecture string for the current platform."""

    if _platform.OPERATING_SYSTEM == "linux":
        return "linux-x64"
    if _platform.OPERATING_SYSTEM == "darwin":
        return "osx-arm"
    if _platform.OPERATING_SYSTEM == "windows":
        return "win-x64"

    print(
        f"Unsupported operating system '{_platform.OPERATING_SYSTEM}' for release "
        "installation. Exiting."
    )
    sys.exit(2)


def _resolve_dist_args(args: _argparse.Namespace) -> tuple[str, str]:
    """Resolve dist-url and dist-token from CLI arguments or environment variables."""

    dist_url = args.dist_url or _os.environ.get("DBMAN_DIST_URL")
    dist_token = args.dist_token or _os.environ.get("DBMAN_DIST_TOKEN")

    if not dist_url:
        print("dist-url is required. Provide via --dist-url or DBMAN_DIST_URL. Exiting.")
        sys.exit(1)
    if not dist_token:
        print("dist-token is required. Provide via --dist-token or DBMAN_DIST_TOKEN. Exiting.")
        sys.exit(1)

    return dist_url, dist_token


def _add_dist_arguments(parser: _argparse.ArgumentParser) -> None:
    """Add ``--dist-url`` and ``--dist-token`` arguments to a subparser."""

    parser.add_argument(
        "--dist-url",
        type=str,
        default=None,
        help=(
            "Base URL of the distribution server. "
            "Falls back to DBMAN_DIST_URL environment variable."
        ),
    )
    parser.add_argument(
        "--dist-token",
        type=str,
        default=None,
        help=(
            "Authentication token for the distribution server. "
            "Falls back to DBMAN_DIST_TOKEN environment variable."
        ),
    )


def _handle_update_self(args: _argparse.Namespace) -> None:
    """Handle the ``update-self`` command."""

    dist_url, dist_token = _resolve_dist_args(args)
    client = _DistClient(dist_url, dist_token)

    print("Querying latest installer version...")
    latest = client.get_latest_installer_version()
    if latest is None:
        print("No installer versions found on the server.")
        sys.exit(0)

    latest_version, latest_id = latest
    current_version = _semver.Version.parse(_DBMAN_INSTALLER_VERSION)

    print(f"Current installer version: {current_version}")
    print(f"Latest installer version:  {latest_version}")

    if latest_version <= current_version:
        print("Already up to date.")
        return

    print(f"Downloading installer v{latest_version}...")
    arch = _get_current_architecture()
    dest = client.download_installer(latest_id, arch, _pathlib.Path.cwd())
    print(f"Downloaded: {dest}")


def _handle_download_latest(args: _argparse.Namespace) -> None:
    """Handle the ``download-latest`` command."""

    dist_url, dist_token = _resolve_dist_args(args)
    client = _DistClient(dist_url, dist_token)

    print("Checking installed dbman version...")
    try:
        result = _subprocess.run(
            ["dbman", "--version"],
            capture_output=True,
            text=True,
            check=True,
        )
        current_version = _semver.Version.parse(result.stdout.strip())
        print(f"Installed version: {current_version}")
    except FileNotFoundError:
        print("dbman is not installed. Will download the latest version.")
        current_version = None
    except _subprocess.CalledProcessError as e:
        print(f"Error running 'dbman --version': {e}")
        sys.exit(1)

    print("Querying latest app version...")
    latest = client.get_latest_app_version()
    if latest is None:
        print("No app versions found on the server.")
        sys.exit(0)

    latest_version, latest_id = latest
    print(f"Latest app version: {latest_version}")

    if current_version is not None and latest_version <= current_version:
        print("Already up to date.")
        return

    print(f"Downloading app v{latest_version}...")
    arch = _get_current_architecture()
    dest = client.download_app(latest_id, arch, _pathlib.Path.cwd())
    print(f"Downloaded: {dest}")


def run_cli():
    """Run the CLI for the installation script."""

    parser = _argparse.ArgumentParser(
        description="Manage dbman installations from release builds."
    )

    parser.add_argument(
        "--system",
        action="store_true",
        help="Manage system-wide application installations (requires root privileges).",
    )

    parser.add_argument(
        "--version", action="version", version=f"%(prog)s {_read_installer_version()}"
    )

    subparsers = parser.add_subparsers(dest="command", required=True)

    install_parser = subparsers.add_parser("install", help="Install the application.")
    install_parser.add_argument(
        "--source-dir",
        type=str,
        help="Path to the release build directory. If not given, the current directory is used.",
    )
    install_parser.add_argument(
        "--versions-to-keep",
        type=int,
        help="Number of previous versions to keep. If not specified, all versions are kept.",
        default=None,
    )
    install_parser.add_argument(
        "--force",
        action="store_true",
        help="Force reinstallation even if the same version is already installed.",
    )

    rollback_parser = subparsers.add_parser("rollback", help="Rollback to a previous version.")
    rollback_parser.add_argument(
        "version",
        type=str,
        help="The version to which to roll back.",
    )

    _ = subparsers.add_parser("uninstall", help="Uninstall the application.")

    update_self_parser = subparsers.add_parser(
        "update-self", help="Update the installer to the latest version."
    )
    _add_dist_arguments(update_self_parser)

    download_latest_parser = subparsers.add_parser(
        "download-latest", help="Download the latest app version."
    )
    _add_dist_arguments(download_latest_parser)

    args = parser.parse_args()

    if args.command == "update-self":
        _handle_update_self(args)
        return

    if args.command == "download-latest":
        _handle_download_latest(args)
        return

    if args.system and not _platform.is_user_admin():
        print("System-wide installations require root privileges. Exiting.")
        sys.exit(1)

    if _platform.OPERATING_SYSTEM == "linux":
        manager = _ml.LinuxReleaseAppManager(system_wide=args.system)
    elif _platform.OPERATING_SYSTEM == "windows":
        manager = _mw.WindowsReleaseAppManager(system_wide=args.system)
    elif _platform.OPERATING_SYSTEM == "darwin":
        manager = _mm.MacReleaseAppManager(system_wide=args.system)
    else:
        print(
            f"Unsupported operating system '{_platform.OPERATING_SYSTEM}' for release "
            "installation. Exiting."
        )
        sys.exit(2)

    if args.command == "install":
        if args.versions_to_keep is not None and args.versions_to_keep < 1:
            print("The number of versions to keep must be at least 1. Exiting.")
            sys.exit(1)

        manager.release_install(
            args.source_dir, force=args.force, keep_latest=args.versions_to_keep
        )
    elif args.command == "rollback":
        manager.rollback(args.version)
    elif args.command == "uninstall":
        manager.uninstall()
    else:
        print(f"Unknown command '{args.command}'. Exiting.")
        parser.print_help()
        sys.exit(1)


if __name__ == "__main__":
    run_cli()
