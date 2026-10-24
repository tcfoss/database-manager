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
import shutil as _shutil
import subprocess as _sp
import sys as _sys
import tempfile as _tempfile
import typing as _typing
import xml.etree.ElementTree as _et
import uuid as _uuid
import dbman_dev.utilities.shell_exec as _shell_exec

import attrs as _attrs

_DEFAULT_LICENSE_PATH = _p.Path.home() / "Code" / "Licenses" / "reportgenerator_license_key.txt"
_OPEN_COVER_FILENAME_PATTERN = _re.compile(r"coverage\.opencover\.\d+\.xml", _re.IGNORECASE)
_RAW_COVERAGE_FILENAME_PATTERN = _re.compile(r"coverage\.\w+\.\d+\.xml", _re.IGNORECASE)
_SOLUTION_EXTENSIONS = {".sln", ".slnx", ".slnf"}
_COVERAGE_FILTER_PATH = _shutil.which("unreachable-coverage-filter")

@_attrs.define()
class _RunConfig:
    solution: _p.Path
    test_results_dir: _p.Path
    history_dir: _p.Path | None = None
    license: str | None = None
    keep_raw_coverage_files: bool = False
    release_mode: bool = False
    skip_coverage_guard: bool = False


def _check_reportgenerator_installed():
    """Check if ReportGenerator is installed."""
    try:
        _sp.check_output(["reportgenerator", "-h"], encoding="utf-8")
        print("ReportGenerator is installed.")
    except FileNotFoundError:
        print("ReportGenerator is not installed. Please install it to generate coverage reports.")
        _sys.exit(1)

def _check_coverage_filter_installed():
    """Check if CoverageFilter is installed."""
    if _COVERAGE_FILTER_PATH is None:
        print("UnreachableCoverageFilter is not installed. Please install it to filter coverage reports.")
        _sys.exit(1)

def _remove_empty_subdirs(path: _p.Path):
    """Remove empty subdirectories under the given path."""
    for subdir in path.iterdir():
        if subdir.is_dir() and not subdir.name.startswith(".") and not any(subdir.iterdir()):
            print(f"Removing empty directory: {subdir}")
            subdir.rmdir()

def _remove_raw_coverage_files(coverage_paths: list[str]):
    """Remove raw coverage files after report generation."""
    paths = {_p.Path(coverage_path) for coverage_path in coverage_paths}
    for directory in {_p.Path(coverage_path).parent for coverage_path in coverage_paths}:
        paths.update(path for path in directory.glob("*.xml") if _RAW_COVERAGE_FILENAME_PATTERN.fullmatch(path.name))

    for path in paths:
        try:
            if not path.is_absolute():
                print(f"Absolute path is required. Received relative path: {path}")
                _sys.exit(1)
            print(f"Removing raw coverage file: {path}")
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

    if config.release_mode:
        print("Running tests in release mode.")
        cmd.extend(["--configuration", "Release"])

    _shell_exec.run_output(cmd, cwd=config.solution.parent)

    search_paths = [x for x in config.test_results_dir.glob("*.xml") if _OPEN_COVER_FILENAME_PATTERN.fullmatch(x.name)]
    if not search_paths:
        print("Could not find an OpenCover report in the test results directory.")
        _sys.exit(1)
    file_paths = [str(x) for x in search_paths]

    return file_paths


def _filter_coverage_reports(config: _RunConfig, coverage_files: list[str]) -> list[str]:
    """Filter unreachable-exception guards from Coverlet OpenCover reports."""
    output_dir = _p.Path(coverage_files[0]).parent / "filtered-coverage"
    configuration = "Release" if config.release_mode else "Debug"

    source_root = config.solution.parent
    if source_root.stem == "solutions":
        source_root = source_root.parent
    source_root = source_root / "src"

    project_directories = {project.stem: project.parent for project in source_root.glob("*/*.csproj")}
    module_names: set[str] = set()
    for coverage_file in coverage_files:
        root = _et.parse(coverage_file).getroot()
        module_names.update(
            _p.Path((module_path.text or "").replace("\\", "/")).name
            for module_path in root.findall("./Modules/Module/ModulePath")
            if module_path.text
        )

    module_assemblies: dict[str, _p.Path] = {}
    for module_name in module_names:
        if _p.Path(module_name).is_absolute():
            continue
        project_directory = project_directories.get(_p.Path(module_name).stem)
        if project_directory is None:
            print(f"No source project found for coverage module {module_name}; it may be skipped.")
            continue
        project_output_dir = project_directory / "bin" / configuration / "net10.0"
        if not (project_output_dir / module_name).is_file():
            print(f"Built assembly not found for coverage module {module_name}: {project_output_dir}")
            continue
        module_assemblies[module_name] = project_output_dir / module_name

    if not module_assemblies and any(not _p.Path(name).is_absolute() for name in module_names):
        print(f"Could not locate built source assemblies for coverage modules under {source_root}.")
        _sys.exit(1)

    filtered_files = [str(output_dir / _p.Path(path).name) for path in coverage_files]
    cmd = [
        _typing.cast(str, _COVERAGE_FILTER_PATH),
        *coverage_files,
        "--output-directory",
        str(output_dir),
        "--exclude-regex",
        r"throw new (TcfOss\.)?(DatabaseManager\.)?(Core\.)?(Errors\.)?DefensiveGuardException\(",
        "--source-root",
        str(source_root),
    ]
    print(f"Filtering coverage reports into {output_dir}...")
    with _tempfile.TemporaryDirectory(prefix="dbman-coverage-assemblies-") as assembly_directory:
        for module_name, source_assembly in module_assemblies.items():
            _shutil.copy2(source_assembly, _p.Path(assembly_directory) / module_name)
            source_pdb = source_assembly.with_suffix(".pdb")
            if not source_pdb.is_file():
                raise FileNotFoundError(f"Matching PDB not found for coverage module {module_name}: {source_pdb}")
            _shutil.copy2(source_pdb, _p.Path(assembly_directory) / source_pdb.name)

        cmd.extend(["--assembly-directory", assembly_directory])
        _shell_exec.run(cmd, cwd=config.solution.parent)

    missing_files = [path for path in filtered_files if not _p.Path(path).is_file()]
    if missing_files:
        raise FileNotFoundError(f"Coverage filter did not produce expected report: {missing_files[0]}")

    return filtered_files


def _generate_report(config: _RunConfig, test_results_files: list[str]):
    """Generate the coverage report."""
    output_dir = _p.Path(test_results_files[0]).parent
    print(f"Generating report in {output_dir}...")

    config.test_results_dir = config.test_results_dir or output_dir
    if config.skip_coverage_guard:
        print("Skipping UnreachableCoverageFilter; using unfiltered coverage reports.")
        filtered_files = test_results_files
    else:
        filtered_files = _filter_coverage_reports(config, test_results_files)

    cmd = [
        "reportgenerator",
        f"-reports:{';'.join(filtered_files)}",
        f"-targetdir:{output_dir}",
        "-reporttypes:Html;TextSummary;Cobertura;OpenCover"
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
    parser.add_argument(
        "--release-mode",
        action="store_true",
        help="Run tests in release mode."
    )
    parser.add_argument(
        "--skip-coverage-guard",
        action="store_true",
        help="Skip filtering defensive guards from coverage reports.",
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
        release_mode=args.release_mode,
        skip_coverage_guard=args.skip_coverage_guard,
    )


def main():
    """Main entry point for the script."""
    _check_reportgenerator_installed()
    config = _parse_args()
    if not config.skip_coverage_guard:
        _check_coverage_filter_installed()
    test_results_files = _run_tests(config)
    _generate_report(config, test_results_files)
    if not config.keep_raw_coverage_files:
        _remove_raw_coverage_files(test_results_files)
    if config.test_results_dir:
        _remove_empty_subdirs(config.test_results_dir)


if __name__ == "__main__":
    main()
