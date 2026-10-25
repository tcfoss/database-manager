"""Tests for multi-project INFORMATION_SCHEMA generation."""

from contextlib import redirect_stderr, redirect_stdout
from io import StringIO
from pathlib import Path
import shutil
import subprocess
from tempfile import TemporaryDirectory
from types import SimpleNamespace
import unittest
from unittest.mock import call, patch

from dbman_dev.csharp_code_generation import model_data_loader as loader
from dbman_dev.csharp_code_generation.model_config import ModelConfig
from dbman_dev.infoschema_building import test_data


class ModelDataLoaderTests(unittest.TestCase):
    """Exercise initialization and extraction without Docker."""

    def setUp(self):
        self.directory = TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.projects = []
        for name, schemas in (
            ("Library", ["library_catalog", "library_identity", "library_activity"]),
            ("ExtraTest", ["extra_test"]),
        ):
            source = self.root / name
            (source / "Init").mkdir(parents=True)
            (source / "Init" / "initialize.sh").write_text("true\n", encoding="utf-8")
            self.projects.append(loader.ProjectConfig(name, source, schemas, ["initialize.sh"]))

        container_patch = patch.object(loader.db_container, "MySqlContainer")
        self.container_factory = container_patch.start()
        self.addCleanup(container_patch.stop)
        self.container = self.container_factory.return_value
        self.container.__enter__.return_value = self.container
        self.container.__exit__.return_value = False
        self.container.exec_run.return_value = SimpleNamespace(exit_code=0, output=b"")
        self.container.query.return_value = ()
        self.generator = loader.TestDataGenerator("test-image", [], {})
        output = redirect_stdout(StringIO())
        output.__enter__()
        self.addCleanup(output.__exit__, None, None, None)

    def test_projects_initialize_before_combined_extraction(self):
        self.generator.tables = [ModelConfig("TABLES", [])]
        column = SimpleNamespace(column_name="TABLE_SCHEMA", property_name="TableSchema")
        with patch.object(loader, "get_show_fields_results", return_value=[column]):
            self.generator.generate_data(self.projects)

        expected = []
        for project in self.projects:
            expected.extend(
                [
                    call.exec_run(["rm", "-rf", "/InitialSchemas"], workdir=None),
                    call.exec_run(["mkdir", "/InitialSchemas"], workdir=None),
                    call.upload_file(
                        source_path=str(project.source_path), target_path="/InitialSchemas"
                    ),
                    call.exec_run(
                        ["bash", "-e", "-o", "pipefail", "initialize.sh"],
                        workdir="/InitialSchemas/Init",
                    ),
                ]
            )
        expected.append(
            call.query(
                "SELECT TABLE_SCHEMA FROM `TABLES` WHERE `TABLE_SCHEMA` IN "
                "('library_catalog','library_identity','library_activity','extra_test')"
            )
        )
        actual = [
            entry
            for entry in self.container.mock_calls
            if entry[0] in ("exec_run", "upload_file", "query")
        ]
        self.assertEqual(actual, expected)
        self.container.__enter__.assert_called_once()
        self.container.__exit__.assert_called_once()

    def test_schemata_are_filtered_by_schema_name(self):
        self.generator.tables = [ModelConfig("SCHEMATA", [])]
        column = SimpleNamespace(column_name="SCHEMA_NAME", property_name="SchemaName")
        with patch.object(loader, "get_show_fields_results", return_value=[column]):
            self.generator.generate_data([self.projects[1]])
        self.container.query.assert_called_once_with(
            "SELECT SCHEMA_NAME FROM `SCHEMATA` WHERE `SCHEMA_NAME` IN ('extra_test')"
        )

    def test_global_metadata_queried_once_and_ignored_columns_excluded(self):
        self.generator.tables = [ModelConfig("ENGINES", [], ignored_columns=["COMMENT"])]
        column = SimpleNamespace(
            column_name="ENGINE", property_name="Engine", get_value=lambda value: f'"{value}"'
        )
        ignored = SimpleNamespace(column_name="COMMENT")
        self.container.query.return_value = (("InnoDB",),)
        with patch.object(loader, "get_show_fields_results", return_value=[column, ignored]):
            result = self.generator.generate_data(self.projects)
        self.container.query.assert_called_once_with("SELECT ENGINE FROM `ENGINES`")
        self.assertEqual(
            result, {"Engines": ['            new EnginesEntity { Engine = "InnoDB", },']}
        )

    def test_invalid_projects_fail_before_container_start(self):
        invalid_projects = [
            [],
            [loader.ProjectConfig("Empty", self.root, [], ["initialize.sh"])],
            [loader.ProjectConfig("Missing", self.root / "missing", ["missing"], ["init.sh"])],
            [loader.ProjectConfig("Script", self.root, ["script"], ["missing.sh"])],
            [loader.ProjectConfig("NoScript", self.root, ["script"], [])],
            [self.projects[0], self.projects[0]],
            [
                self.projects[0],
                loader.ProjectConfig(
                    "Overlap", self.projects[1].source_path, ["library_catalog"], ["initialize.sh"]
                ),
            ],
        ]
        for projects in invalid_projects:
            with self.subTest(projects=projects), self.assertRaises(ValueError):
                self.generator.generate_data(projects)
        self.container.__enter__.assert_not_called()

    def test_command_failures_stop_extraction_and_close_container(self):
        for failing_command in range(6):
            with self.subTest(failing_command=failing_command):
                self.container.reset_mock()
                results = [SimpleNamespace(exit_code=0, output=b"")] * failing_command
                results.append(SimpleNamespace(exit_code=1, output=b"initialization failed"))
                self.container.exec_run.side_effect = results
                project_name = "Library" if failing_command < 3 else "ExtraTest"
                with self.assertRaisesRegex(RuntimeError, f"Project {project_name}:.*command"):
                    self.generator.generate_data(self.projects)
                self.container.query.assert_not_called()
                self.container.__exit__.assert_called_once()

    def test_failed_initialization_preserves_output(self):
        output_path = self.root / "TestData.cs"
        output_path.write_text("existing fixture", encoding="utf-8")
        config = loader.ModelDataLoaderConfig(
            "MySql", self.projects, output_path, "test-image", [], "Entities", "Tests"
        )
        self.container.exec_run.return_value = SimpleNamespace(exit_code=1, output=b"failed")
        with self.assertRaises(RuntimeError):
            loader.run(config)
        self.assertEqual(output_path.read_text(encoding="utf-8"), "existing fixture")

    def test_custom_project_and_script_order(self):
        project = self.projects[0]
        (project.source_path / "Init" / "second.sh").write_text("true\n", encoding="utf-8")
        project.name = "ThirdProject"
        project.schemas = ["third_schema"]
        project.initialization_scripts.append("second.sh")
        self.generator.generate_data([project])
        self.assertEqual(
            self.container.exec_run.call_args_list[-2:],
            [
                call(
                    ["bash", "-e", "-o", "pipefail", "initialize.sh"],
                    workdir="/InitialSchemas/Init",
                ),
                call(
                    ["bash", "-e", "-o", "pipefail", "second.sh"], workdir="/InitialSchemas/Init"
                ),
            ],
        )

    def test_generated_class_and_method_layout_unchanged(self):
        output_path = self.root / "TestData.cs"
        config = loader.ModelDataLoaderConfig(
            "MySql", self.projects, output_path, "test-image", [], "Entities", "Tests"
        )
        with patch.object(loader.TestDataGenerator, "generate_data", return_value={"Table": []}):
            loader.run(config)
        output = output_path.read_text(encoding="utf-8")
        self.assertIn("using Entities;", output)
        self.assertIn("namespace Tests;", output)
        self.assertIn("public static class MySqlContextTestData", output)
        self.assertIn("public static TableEntity[] GetTableEntities()", output)


class ProjectSelectionTests(unittest.TestCase):
    """Check default project ownership and CLI selection."""

    def test_defaults_cover_both_projects_for_both_dialects(self):
        for dialect in ("MySql", "MariaDb"):
            with self.subTest(dialect=dialect):
                config = test_data.get_arguments(dialect)
                self.assertEqual(
                    [project.name for project in config.projects], ["Library", "ExtraTest"]
                )
                self.assertEqual(
                    [schema for project in config.projects for schema in project.schemas],
                    ["library_catalog", "library_identity", "library_activity", "extra_test"],
                )
                for project in config.projects:
                    self.assertEqual(
                        project.source_path,
                        test_data.paths.MYSQL_TEST_PROJECTS_DIR / project.name / "Initial",
                    )

    def test_defaults_do_not_share_mutable_configuration(self):
        first = test_data.get_arguments("MySql")
        second = test_data.get_arguments("MySql")
        first.projects[0].schemas.append("another_schema")
        first.projects[0].initialization_scripts.clear()
        first.environment.clear()
        self.assertEqual(len(second.projects[0].schemas), 3)
        self.assertEqual(second.projects[0].initialization_scripts, ["initialize_library.sh"])
        self.assertTrue(second.environment)

    def test_programmatic_projects_are_preserved_including_empty_selection(self):
        project = loader.ProjectConfig("Custom", Path("/custom"), ["custom"], ["custom.sh"])
        projects = [project]
        self.assertIs(test_data.get_arguments("MySql", projects=projects).projects, projects)
        self.assertEqual(test_data.get_arguments("MySql", projects=[]).projects, [])

    def test_cli_project_selection(self):
        for selection, expected in (
            ([], ["Library", "ExtraTest"]),
            (["--projects", "Library"], ["Library"]),
            (["--projects", "ExtraTest"], ["ExtraTest"]),
            (["--projects", "ExtraTest", "Library"], ["ExtraTest", "Library"]),
        ):
            with self.subTest(selection=selection), patch(
                "sys.argv", ["test-data", "MySql", *selection]
            ), patch.object(test_data, "run") as run_mock:
                test_data.run_cli()
                config = run_mock.call_args.args[0]
                self.assertEqual([project.name for project in config.projects], expected)

    def test_cli_rejects_invalid_selection_and_removed_schema_option(self):
        for selection in (
            ["--projects", "Unknown"],
            ["--projects", "Library", "Library"],
            ["--projects"],
            ["--schemas", "library_catalog"],
        ):
            with self.subTest(selection=selection), patch(
                "sys.argv", ["test-data", "MySql", *selection]
            ), patch.object(test_data, "run") as run_mock, redirect_stderr(StringIO()):
                with self.assertRaises(SystemExit) as error:
                    test_data.run_cli()
                self.assertEqual(error.exception.code, 2)
                run_mock.assert_not_called()


class EventInitializationTests(unittest.TestCase):
    """Run the real initialization script with a fake database client."""

    def test_event_clause_follows_server_version_without_modifying_source(self):
        source = test_data.paths.MYSQL_TEST_PROJECTS_DIR / "ExtraTest" / "Initial"
        event_source = source / "Schema" / "event_disable_on_slave.sql"
        original = event_source.read_text(encoding="utf-8")
        bootstrap = """
server_version_value=$2
mysql-with-charset() {
    if [[ "$*" == *"SELECT VERSION()"* ]]; then
        if [[ "$server_version_value" == query-failure ]]; then return 31; fi
        printf '%s\\n' "$server_version_value"
    elif [[ "$*" != *--execute=* ]]; then
        payload=$(cat)
        if [[ "$server_version_value" == import-failure && "$payload" == *"CREATE EVENT event_disable_on_slave"* ]]; then
            return 33
        fi
        printf '%s\\n' "$payload"
    fi
}
if [[ "$server_version_value" == sed-failure ]]; then
    sed() { return 32; }
fi
source "$1"
"""
        with TemporaryDirectory() as directory:
            staged = Path(directory) / "InitialSchemas"
            shutil.copytree(source, staged)
            script = staged / "Init" / "initialize_db.sh"
            script.write_text(
                script.read_text(encoding="utf-8").replace("/InitialSchemas", str(staged)),
                encoding="utf-8",
            )
            for version, expected, absent in (
                ("8.4.6", "DISABLE ON REPLICA", "DISABLE ON SLAVE"),
                ("11.8.2-MariaDB-ubu2404", "DISABLE ON SLAVE", "DISABLE ON REPLICA"),
            ):
                with self.subTest(version=version):
                    result = subprocess.run(
                        ["bash", "-c", bootstrap, "test-init", str(script), version],
                        capture_output=True,
                        text=True,
                        check=True,
                    )
                    self.assertIn("CREATE EVENT event_disable_on_slave", result.stdout)
                    self.assertIn(expected, result.stdout)
                    self.assertNotIn(absent, result.stdout)
            for failure, expected_code in (
                ("query-failure", 31),
                ("sed-failure", 32),
                ("import-failure", 33),
            ):
                with self.subTest(failure=failure):
                    result = subprocess.run(
                        ["bash", "-c", bootstrap, "test-init", str(script), failure],
                        capture_output=True,
                        text=True,
                        check=False,
                    )
                    self.assertEqual(result.returncode, expected_code)
            self.assertEqual(
                (staged / "Schema" / "event_disable_on_slave.sql").read_text(encoding="utf-8"),
                original,
            )
        self.assertEqual(event_source.read_text(encoding="utf-8"), original)


if __name__ == "__main__":
    unittest.main()
