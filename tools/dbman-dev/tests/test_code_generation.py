"""Focused checks for runtime configuration and initialization without Docker or I/O."""

import importlib
from contextlib import redirect_stderr, redirect_stdout
from io import StringIO
from types import SimpleNamespace
import unittest
from unittest.mock import MagicMock, call, mock_open, patch

from dbman_dev.code_generation import containers, generate_models_and_data, main, projects
from dbman_dev.code_generation.dialect import SqlDialect


class RuntimeConfigTests(unittest.TestCase):
    def test_no_schemas_resolves_all_registered_schemas(self):
        for dialect in SqlDialect:
            with self.subTest(dialect=dialect):
                config = projects.get_config(dialect, [])
                expected = [
                    schema for item in config.inits for schema in item.provided_schemas
                ]
                self.assertTrue(expected)
                self.assertEqual(config.project_config.schemas, expected)

    def test_one_schema_initializes_its_project_but_only_extracts_requested_schema(self):
        for dialect in SqlDialect:
            with self.subTest(dialect=dialect):
                requested = ["library_activity"]
                config = projects.get_config(dialect, requested)
                self.assertEqual(len(config.inits), 1)
                self.assertEqual(config.project_config.schemas, requested)
                requested.append("library_catalog")
                self.assertEqual(config.project_config.schemas, ["library_activity"])

    def test_invalid_schema_selection_fails_before_container_construction(self):
        for dialect in SqlDialect:
            for schemas in (
                ["unknown"],
                ["library_catalog", "unknown"],
                ["library_catalog", "library_catalog"],
                [""],
            ):
                with self.subTest(dialect=dialect, schemas=schemas):
                    with (
                        patch.object(projects.dbc, "MySqlContainer") as mysql,
                        patch.object(projects.dbc, "PostgresContainer") as postgres,
                    ):
                        with self.assertRaises(ValueError):
                            projects.get_config(dialect, schemas)
                        mysql.assert_not_called()
                        postgres.assert_not_called()


class CliTests(unittest.TestCase):
    def setUp(self):
        run_patch = patch.object(main.generate_models_and_data, "run")
        self.run = run_patch.start()
        self.addCleanup(run_patch.stop)

    def test_default_invocation_dispatches_each_dialect_with_all_outputs(self):
        for dialect in SqlDialect:
            with self.subTest(dialect=dialect):
                self.run.reset_mock()
                self._invoke([dialect.value])
                self.run.assert_called_once()
                config = self.run.call_args.args[0]
                self.assertEqual(config.dialect, dialect)
                self.assertTrue(config.project_config.schemas)
                self.assertIsNotNone(config.project_config.entity_dir)
                self.assertIsNotNone(config.project_config.config_dir)
                self.assertIsNotNone(config.project_config.data_file)

    def test_intermixed_skip_models_preserves_schema_selection_and_data_output(self):
        self._invoke(["mysql", "library_catalog", "--skip-models", "extra_test"])
        config = self.run.call_args.args[0]
        self.assertEqual(config.project_config.schemas, ["library_catalog", "extra_test"])
        self.assertEqual(len(config.inits), 2)
        self.assertIsNone(config.project_config.entity_dir)
        self.assertIsNone(config.project_config.config_dir)
        self.assertIsNotNone(config.project_config.data_file)

    def test_skip_data_preserves_models_and_schema_selection(self):
        self._invoke(["--skip-data", "postgres", "library_activity"])
        config = self.run.call_args.args[0]
        self.assertEqual(config.project_config.schemas, ["library_activity"])
        self.assertIsNotNone(config.project_config.entity_dir)
        self.assertIsNotNone(config.project_config.config_dir)
        self.assertIsNone(config.project_config.data_file)

    def test_invalid_arguments_are_usage_errors_without_generation(self):
        for arguments, message in (
            ([], "required"),
            (["mssql"], "invalid choice"),
            (["mysql", "unknown"], "Unknown schemas"),
            (["postgres", "extra_test"], "Unknown schemas"),
            (["mysql", "library_catalog", "library_catalog"], "distinct"),
            (["mysql", "--output-path", "output.cs"], "unrecognized arguments"),
        ):
            with self.subTest(arguments=arguments):
                stderr = StringIO()
                with redirect_stderr(stderr), self.assertRaises(SystemExit) as error:
                    self._invoke(arguments)
                self.assertEqual(error.exception.code, 2)
                self.assertIn(message, stderr.getvalue())
                self.run.assert_not_called()

    def test_both_skip_flags_fail_before_configuration(self):
        stderr = StringIO()
        with (
            patch.object(main.projects, "get_config") as get_config,
            redirect_stderr(stderr),
            self.assertRaises(SystemExit) as error,
        ):
            self._invoke(["mysql", "--skip-models", "--skip-data"])
        self.assertEqual(error.exception.code, 2)
        self.assertIn("cannot be used together", stderr.getvalue())
        get_config.assert_not_called()
        self.run.assert_not_called()

    def test_help_and_version_do_not_configure_or_generate(self):
        for argument in ("--help", "--version"):
            with self.subTest(argument=argument):
                stdout = StringIO()
                with (
                    patch.object(main.projects, "get_config") as get_config,
                    redirect_stdout(stdout),
                    self.assertRaises(SystemExit) as error,
                ):
                    self._invoke([argument])
                self.assertEqual(error.exception.code, 0)
                self.assertTrue(stdout.getvalue())
                get_config.assert_not_called()
                self.run.assert_not_called()

    def test_generation_errors_are_not_misreported_as_usage_errors(self):
        self.run.side_effect = ValueError("invalid model")
        with self.assertRaisesRegex(ValueError, "invalid model"):
            self._invoke(["mysql"])

    def _invoke(self, arguments: list[str]) -> None:
        with patch("sys.argv", ["dbman-dev-codegen", *arguments]):
            main.run_cli()


class InitializationTests(unittest.TestCase):
    def setUp(self):
        self.config = projects.get_config(SqlDialect.MYSQL, [])
        self.config.container = MagicMock(spec=containers.DbContainerBase)
        self.container = self.config.container
        self.container.exec_run.return_value = SimpleNamespace(exit_code=0, output=b"")
        generator_patch = patch.object(generate_models_and_data.pg, "ProjectGenerator")
        self.generator = generator_patch.start()
        self.addCleanup(generator_patch.stop)

    def test_each_project_is_staged_separately_before_generation(self):
        generate_models_and_data.run(self.config)
        expected = [call.start(), call.connect()]
        for item in self.config.inits:
            expected.extend([
                call.exec_run(["rm", "-rf", "/InitialSchemas"], workdir=None),
                call.exec_run(["mkdir", "-p", "/InitialSchemas"], workdir=None),
                call.upload_file(source_path=str(item.source_path), target_path="/InitialSchemas"),
            ])
            expected.extend(
                call.exec_run(
                    ["bash", "-e", "-o", "pipefail", script],
                    workdir="/InitialSchemas/Init",
                )
                for script in item.initialization_scripts
            )
        expected.append(call.stop())
        self.assertEqual(self.container.mock_calls, expected)
        self.generator.return_value.generate.assert_called_once()

    def test_models_only_does_not_initialize_fixture_projects(self):
        self.config.project_config.data_file = None
        generate_models_and_data.run(self.config)
        self.container.exec_run.assert_not_called()
        self.container.upload_file.assert_not_called()
        self.generator.return_value.generate.assert_called_once()
        self.container.stop.assert_called_once()

    def test_failed_staging_or_script_aborts_generation_and_cleans_up(self):
        for failure_position in range(3):
            with self.subTest(failure_position=failure_position):
                self.container.reset_mock()
                self.generator.reset_mock()
                self.container.exec_run.side_effect = [
                    *[SimpleNamespace(exit_code=0, output=b"") for _ in range(failure_position)],
                    SimpleNamespace(exit_code=7, output=b"initialization failed"),
                ]
                with self.assertRaisesRegex(RuntimeError, "initialization failed"):
                    generate_models_and_data.run(self.config)
                self.generator.assert_not_called()
                self.container.stop.assert_called_once()

    def test_generation_failure_still_cleans_up(self):
        self.generator.return_value.generate.side_effect = ValueError("invalid model")
        with self.assertRaisesRegex(ValueError, "invalid model"):
            generate_models_and_data.run(self.config)
        self.container.stop.assert_called_once()

    def test_importing_orchestrator_has_no_runtime_side_effects(self):
        with patch.object(projects, "get_config") as get_config:
            importlib.reload(generate_models_and_data)
        get_config.assert_not_called()
        self.container.start.assert_not_called()


class ContainerTests(unittest.TestCase):
    def test_root_connection_password_matches_container_initialization(self):
        for root_password in (None, "", "explicit-root-password"):
            with self.subTest(root_password=root_password):
                client = MagicMock()
                client.containers.get.side_effect = containers.docker_errors.NotFound("missing")
                client.containers.create.return_value.status = "running"
                container = containers.MySqlContainer(
                    "test", user="root", password="connection-password",
                    root_password=root_password,
                )
                with (
                    patch.object(containers.docker, "from_env", return_value=client),
                    patch.object(containers.pymysql, "connect") as connect,
                ):
                    try:
                        container.start()
                        container.connect()
                        environment = client.containers.create.call_args.kwargs["environment"]
                        password = connect.call_args.kwargs["password"]
                        if password:
                            self.assertEqual(environment["MYSQL_ROOT_PASSWORD"], password)
                            self.assertNotIn("MYSQL_ALLOW_EMPTY_PASSWORD", environment)
                        else:
                            self.assertEqual(environment["MYSQL_ALLOW_EMPTY_PASSWORD"], "true")
                            self.assertNotIn("MYSQL_ROOT_PASSWORD", environment)
                    finally:
                        container.stop()

    def test_upload_cleans_archive_directory_and_reports_rejection(self):
        for accepted in (True, False):
            with self.subTest(accepted=accepted):
                container = containers.MySqlContainer("test")
                docker_container = MagicMock()
                container._container = docker_container
                docker_container.put_archive.return_value = accepted
                with (
                    patch.object(containers.tempfile, "TemporaryDirectory") as temporary,
                    patch.object(
                        containers.shutil, "make_archive", return_value="/temporary/schemas.tar"
                    ) as make_archive,
                    patch("builtins.open", mock_open(read_data=b"archive")) as open_archive,
                ):
                    temporary.return_value.__enter__.return_value = "/temporary"
                    if accepted:
                        container.upload_file("/source", "/InitialSchemas")
                    else:
                        with self.assertRaisesRegex(RuntimeError, "Failed to upload"):
                            container.upload_file("/source", "/InitialSchemas")
                    make_archive.assert_called_once_with(
                        "/temporary/schemas", "tar", "/source", ""
                    )
                    open_archive.assert_called_once_with("/temporary/schemas.tar", "rb")
                    temporary.return_value.__exit__.assert_called_once()


if __name__ == "__main__":
    unittest.main()
