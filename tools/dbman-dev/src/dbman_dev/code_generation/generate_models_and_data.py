"""Initialize the selected fixture projects and generate their catalog outputs."""

import dbman_dev.code_generation.containers as dbc
import dbman_dev.code_generation.project_generator as pg
import dbman_dev.code_generation.projects as p


def _exec_checked(
    container: dbc.DbContainerBase, command: list[str], workdir: str | None = None
) -> None:
    result = container.exec_run(command, workdir=workdir)
    if result.exit_code != 0:
        output = result.output.decode("utf-8", errors="replace")
        raise RuntimeError(
            f"Initialization command {command!r} failed with exit code {result.exit_code}:\n{output}"
        )


def run(config: p.RuntimeConfig) -> None:
    """Generate outputs using one connection and one container lifecycle."""
    try:
        config.container.start()
        config.container.connect()
        if config.project_config.data_file is not None:
            for item in config.inits:
                _exec_checked(config.container, ["rm", "-rf", "/InitialSchemas"])
                _exec_checked(config.container, ["mkdir", "-p", "/InitialSchemas"])
                config.container.upload_file(
                    source_path=str(item.source_path), target_path="/InitialSchemas"
                )
                for init_script in item.initialization_scripts:
                    _exec_checked(
                        config.container,
                        ["bash", "-e", "-o", "pipefail", init_script],
                        workdir="/InitialSchemas/Init",
                    )
        generator = pg.ProjectGenerator(
            config.project_config, config.container, config.type_mapper
        )
        generator.generate()
    finally:
        config.container.stop()
