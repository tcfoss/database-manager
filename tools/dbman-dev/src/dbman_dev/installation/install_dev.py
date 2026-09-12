"""Installs the TcfOss.DatabaseManager.App application on Linux systems."""

import argparse
import sys

from dbman_dev.utilities.platform import IS_UNIX_LIKE, is_user_admin

from dbman_dev.installation.dev_app_manager_linux import LinuxDevAppManager
from dbman_dev.installation.dev_app_manager_windows import WindowsDevAppManager

def run_cli():
    """Run the CLI for the installation script."""

    parser = argparse.ArgumentParser(description="Manage dbman installations.")

    parser.add_argument(
        "--system",
        action="store_true",
        help="Manage system-wide application installations (requires root privileges).",
    )

    subparsers = parser.add_subparsers(dest="command", required=True)

    install_parser = subparsers.add_parser("install", help="Install the application.")
    install_parser.add_argument(
        "--self-contained",
        action="store_true",
        help="Install the application as self-contained (includes .NET runtime).",
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

    args = parser.parse_args()

    if IS_UNIX_LIKE:
        manager = LinuxDevAppManager(system_wide=args.system)
    else:
        manager = WindowsDevAppManager(system_wide=args.system)

    if args.system and not is_user_admin():
        print("System-wide installations require root privileges. Exiting.")
        sys.exit(1)

    if args.command == "install":
        if args.versions_to_keep is not None and args.versions_to_keep < 1:
            print("The number of versions to keep must be at least 1. Exiting.")
            sys.exit(1)

        manager.install(
            framework_dependent=not args.self_contained,
            keep_latest=args.versions_to_keep,
            force=args.force,
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
