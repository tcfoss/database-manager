"""Run a code-coverage test and generate a report, using Microsoft.Testing.Platform (MTP).

This is a variant of ``generate_report.py`` for repositories whose test projects have
moved to xUnit.net v3 4.x, which dropped VSTest support in favor of exclusive MTP support.
`dotnet test` in MTP mode (enabled via the `test.runner` setting in `global.json`) does not
understand VSTest-only flags such as `--settings <runsettings>` or `-l <logger>`, and requires
`--solution`/`--project` instead of a positional path. Coverage collection is instead
configured per test project (e.g. via `testconfig.json` and the `--coverlet` MTP extension
argument), so no runsettings file is passed here.

The tool can be installed using
```
dotnet tool install --global dotnet-reportgenerator-globaltool
```
"""

import argparse as _ap
import os as _os
import pathlib as _p
import re as _re
import subprocess as _sp
import sys as _sys
import dbman_dev.utilities.shell_exec as _shell_exec
import uuid as _uuid

import attrs as _attrs

_DEFAULT_LICENSE_PATH = _p.Path.home() / "Code" / "Licenses" / "reportgenerator_license_key.txt"
# coverlet.MTP names reports coverage.cobertura.<DDMMYYhhmmssfff>.xml (timestamp of the run)
_FILENAME_PATTERN = _re.compile(r"coverage\.cobertura\.\d+\.xml", _re.IGNORECASE)
_SOLUTION_EXTENSIONS = {".sln", ".slnx", ".slnf"}


@_attrs.define()
class _RunConfig:
    solution: _p.Path
    test_results_dir: _p.Path
    history_dir: _p.Path | None = None
    license: str | None = None
    keep_raw_coverage_files: bool = False


def _check_reportgenerator_installed():
    """Check if ReportGenerator is installed."""
    try:
        _sp.check_output(["reportgenerator", "-h"], encoding="utf-8")
        print("ReportGenerator is installed.")
    except FileNotFoundError:
        print("ReportGenerator is not installed. Please install it to generate coverage reports.")
        _sys.exit(1)


def _remove_empty_subdirs(path: _p.Path):
    """Remove empty subdirectories under the given path."""
    for subdir in path.iterdir():
        if subdir.is_dir() and not subdir.name.startswith(".") and not any(subdir.iterdir()):
            print(f"Removing empty directory: {subdir}")
            subdir.rmdir()

def _remove_raw_coverage_files(coverage_paths: list[str]):
    """Remove raw coverage files after report generation."""
    for coverage_path in coverage_paths:
        path = _p.Path(coverage_path)
        try:
            if not path.is_absolute():
                print(f"Absolute path is required. Received relative path: {coverage_path}")
                _sys.exit(1)
            print(f"Removing raw coverage file: {coverage_path}")
            path.unlink()
        except Exception as e:
            print(f"Warning: Could not remove coverage file {path}: {e}")


def _get_solution_file(cwd: _p.Path, arg_solution: str | None) -> _p.Path:
    """Attempt to find a solution or project file."""
    if arg_solution:
        solution_path = _p.Path(arg_solution)
        if not solution_path.is_absolute():
            solution_path = solution_path.resolve()
        return solution_path

    if arg_solution := _os.environ.get("DBMAN_DEV_COVERAGE_SOLUTION"):
        solution_path = _p.Path(arg_solution)
        if not solution_path.is_absolute():
            solution_path = solution_path.resolve()
        return solution_path

    def search_dir_for_solution(dir_path: _p.Path) -> _p.Path | None:
        print(f"Searching for solution or project file in {dir_path}...")
        for candidate in dir_path.glob("*.sln*"):
            print(f"Found solution file: {candidate}")
            if candidate.is_file():
                if not candidate.is_absolute():
                    candidate = candidate.resolve()
                return candidate
        for candidate in dir_path.glob("*.csproj"):
            print(f"Found project file: {candidate}")
            if candidate.is_file():
                if not candidate.is_absolute():
                    candidate = candidate.resolve()
                return candidate
        return None

    if solution := search_dir_for_solution(cwd):
        return solution

    print("No solution or project file found.")
    _sys.exit(1)


def _get_report_generator_license(arg_license: str | None) -> str | None:
    """Attempt to find a ReportGenerator license key."""
    if arg_license:
        arg_license_path = _p.Path(arg_license)
        if arg_license_path.is_file():
            with open(arg_license_path, "r", encoding="utf-8") as f:
                license_key = f.read().strip()
                print(f"Found ReportGenerator license key in {arg_license_path}")
                return license_key
        return arg_license.strip()

    if env_license := _os.environ.get("DBMAN_DEV_REPORTGENERATOR_LICENSE"):
        return env_license

    if arg_license := _os.environ.get("DBMAN_DEV_REPORTGENERATOR_LICENSE_PATH"):
        env_license_path = _p.Path(arg_license)
        if env_license_path.is_file():
            with open(env_license_path, "r", encoding="utf-8") as f:
                license_key = f.read().strip()
                print(f"Found ReportGenerator license key in {env_license_path}")
                return license_key

    license_path = _DEFAULT_LICENSE_PATH
    if license_path.is_file():
        with open(license_path, "r", encoding="utf-8") as f:
            license_key = f.read().strip()
            print(f"Found ReportGenerator license key in {license_path}")
            return license_key

    print("ReportGenerator license key not found.")
    return None


def _validate_or_create_directory(path: _p.Path, description: str) -> _p.Path:
    """Validate that the given path is a directory, or create it if it doesn't exist."""
    if path.exists():
        if not path.is_dir():
            print(f"Specified {description} path exists but is not a directory: {path}")
            _sys.exit(1)
        print(f"Using existing {description} directory at {path}")
        return path.resolve()

    path.mkdir(parents=False)
    print(f"Created {description} directory at {path}")
    return path.resolve()


def _get_history_dir(solution_dir: _p.Path, arg_history_path: str | None) -> _p.Path:
    """Get the directory for storing coverage history."""
    if arg_history_path:
        history_dir = _p.Path(arg_history_path)
        return _validate_or_create_directory(history_dir, "coverage history")

    if arg_history_path := _os.environ.get("DBMAN_DEV_COVERAGE_HISTORY_DIR"):
        history_dir = _p.Path(arg_history_path)
        return _validate_or_create_directory(history_dir, "coverage history")

    history_dir = solution_dir / "coverage-history"
    return _validate_or_create_directory(history_dir, "coverage history")


def _get_test_results_dir(arg_results_path: str | None) -> _p.Path:
    """Get the directory where test results are stored."""
    if arg_results_path:
        results_path = _p.Path(arg_results_path)
        return _validate_or_create_directory(results_path, "test results")

    return _validate_or_create_directory(_p.Path("test-results") / str(_uuid.uuid4()), "test results")


def _run_tests(config: _RunConfig) -> list[str]:
    """Run the tests and return the path to the test results file."""
    # MTP mode of `dotnet test` requires --solution/--project instead of a positional path.
    target_flag = "--solution" if config.solution.suffix in _SOLUTION_EXTENSIONS else "--project"

    print(f"Using test results directory: {config.test_results_dir}")
    cmd = [
        "dotnet",
        "test",
        target_flag,
        str(config.solution),
        "--results-directory",
        str(config.test_results_dir)
    ]
    _shell_exec.run_output(cmd, cwd=config.solution.parent)

    search_paths = [x for x in config.test_results_dir.glob("*.xml") if _FILENAME_PATTERN.search(str(x))]
    if not search_paths:
        print("Could not find test results file in test results directory.")
        _sys.exit(1)
    file_paths = [str(x) for x in search_paths]

    return file_paths


def _generate_report(config: _RunConfig, test_results_files: list[str]):
    """Generate the coverage report."""
    output_dir = _p.Path(test_results_files[0]).parent
    print(f"Generating report in {output_dir}...")

    config.test_results_dir = config.test_results_dir or output_dir

    cmd = [
        "reportgenerator",
        f"-reports:{';'.join(test_results_files)}",
        f"-targetdir:{output_dir}",
        "-reporttypes:Html;TextSummary;Cobertura"
    ]
    if config.history_dir:
        print(f"Using specified history directory: {config.history_dir}")
        cmd.append(f"-historydir:{config.history_dir}")

    if config.license:
        cmd.append(f"-license:{config.license}")

    print("Running ReportGenerator...")
    _shell_exec.run(cmd, cwd=config.solution.parent)

    print("Report generation complete.")


def _parse_args() -> _RunConfig:
    """Parse command-line arguments."""
    parser = _ap.ArgumentParser(
        description="Generate a code coverage report using ReportGenerator (MTP-mode dotnet test)."
    )
    parser.add_argument("--solution", help="Path to the solution or project file to test.")
    parser.add_argument("--license", help="ReportGenerator license key or path to license file.")
    parser.add_argument(
        "--history-dir",
        help="Directory to store coverage history for tracking coverage changes over time.",
    )
    parser.add_argument(
        "--test-results-dir",
        help="Directory where test results are stored."
    )
    parser.add_argument(
        "--keep-raw-coverage-files",
        action="store_true",
        help="Keep raw coverage files after report generation.",
    )
    args = parser.parse_args()

    solution = _get_solution_file(_p.Path.cwd(), args.solution)
    test_results_dir = _get_test_results_dir(args.test_results_dir)

    return _RunConfig(
        solution=solution,
        license=_get_report_generator_license(args.license),
        history_dir=_get_history_dir(solution.parent, args.history_dir),
        test_results_dir=test_results_dir,
        keep_raw_coverage_files=args.keep_raw_coverage_files,
    )


def main():
    """Main entry point for the script."""
    _check_reportgenerator_installed()
    config = _parse_args()
    test_results_files = _run_tests(config)
    _generate_report(config, test_results_files)
    if not config.keep_raw_coverage_files:
        _remove_raw_coverage_files(test_results_files)
    if config.test_results_dir:
        _remove_empty_subdirs(config.test_results_dir)


if __name__ == "__main__":
    main()
