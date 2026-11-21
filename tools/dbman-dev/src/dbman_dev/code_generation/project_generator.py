"""Generate project data files."""

import io
import os
from contextlib import ExitStack
from pathlib import Path
from tempfile import NamedTemporaryFile

import dbman_dev.code_generation.containers as dbc
import dbman_dev.code_generation.project_config as pc
import dbman_dev.code_generation.structure_queryer as sq
import dbman_dev.code_generation.data_queryer as dq
import dbman_dev.code_generation.cs_writer as cw
import dbman_dev.code_generation.table_config as tc
import dbman_dev.code_generation.data_types as dt


class ProjectGenerator:
    """Generate project data files."""

    def __init__(
        self,
        project_config: pc.ProjectConfig,
        container: dbc.DbContainerBase,
        type_mapper: dt.TypeMapper,
    ):
        self.project_config = project_config
        self.container = container

        self.structure_queryer = sq.StructureQueryer(
            self.project_config.dialect, self.project_config.catalog, type_mapper
        )
        self.data_queryer = dq.DataQueryer(
            self.project_config.schemas, self.project_config.default_schema_predicate_lhs
        )

    def generate(self) -> None:
        """Generate the files."""

        outputs = self._generate_outputs()

        paths = [path.resolve() for path, _ in outputs]
        if len(set(paths)) != len(paths):
            raise ValueError("Generated output paths must be distinct")

        with ExitStack() as cleanup:
            staged: list[tuple[Path, Path]] = []
            for path, content in outputs:
                path.parent.mkdir(parents=True, exist_ok=True)
                with NamedTemporaryFile(
                    mode="w",
                    encoding="utf-8",
                    dir=path.parent,
                    prefix=f".{path.name}.",
                    suffix=".tmp",
                    delete=False,
                ) as stream:
                    temporary_path = Path(stream.name)
                    cleanup.callback(temporary_path.unlink, missing_ok=True)
                    stream.write(content)
                mode = path.stat().st_mode & 0o777 if path.exists() else 0o644
                temporary_path.chmod(mode)
                staged.append((temporary_path, path))
            for temporary_path, path in staged:
                os.replace(temporary_path, path)

    def _generate_outputs(self) -> list[tuple[Path, str]]:
        models = [
            self.structure_queryer.get_model(self.container, table)
            for table in self.project_config.tables
        ]
        if len({model.model_name for model in models}) != len(models):
            raise ValueError("Generated model names must be distinct")

        outputs: list[tuple[Path, str]] = []
        data_stream = io.StringIO()
        data_writer = cw.CsWriter(data_stream)

        if self.project_config.data_file is not None:
            write_model_data_header(
                data_writer,
                self.project_config.data_namespace,
                self.project_config.data_classname,
                self.project_config.entity_namespace,
            )

        for i, model in enumerate(models):

            if self.project_config.entity_dir is not None:
                with io.StringIO() as stream:
                    write_model(cw.CsWriter(stream), model, self.project_config.entity_namespace)
                    outputs.append(
                        (
                            self.project_config.entity_dir / f"{model.model_name}.cs",
                            stream.getvalue(),
                        )
                    )

            if self.project_config.config_dir is not None:
                with io.StringIO() as stream:
                    write_config(
                        cw.CsWriter(stream),
                        model,
                        self.project_config.config_namespace,
                        self.project_config.entity_namespace,
                    )
                    outputs.append(
                        (
                            self.project_config.config_dir / f"{model.config_name}.cs",
                            stream.getvalue(),
                        )
                    )

            if self.project_config.data_file is not None:
                data = self.data_queryer.get_data(self.container, model)
                if i:
                    data_writer.write_blank()
                write_model_data_function(data_writer, model, data)

        if self.project_config.data_file is not None:
            data_writer.close_block()
            outputs.append((self.project_config.data_file, data_stream.getvalue()))

        data_stream.close()
        return outputs


def write_model(writer: cw.CsWriter, model: tc.ModelConfig, namespace: str) -> None:
    writer.write_header()
    writer.write_line(f"namespace {namespace};")
    writer.write_blank()
    writer.write_line(f"public class {model.model_name}")
    writer.open_block()
    for field in model.fields:
        writer.write_line(field.cs_model_declaration)
    writer.close_block()


def write_config(
    writer: cw.CsWriter, model: tc.ModelConfig, namespace: str, entity_namespace: str
) -> None:
    writer.write_header()
    writer.write_line("using Microsoft.EntityFrameworkCore;")
    writer.write_line("using Microsoft.EntityFrameworkCore.Metadata.Builders;")
    writer.write_line(f"using {entity_namespace};")
    writer.write_blank()

    writer.write_line(f"namespace {namespace};")
    writer.write_blank()

    writer.write_line(
        f"public class {model.config_name} : IEntityTypeConfiguration<{model.model_name}>"
    )
    writer.open_block()
    writer.write_line(f"public void Configure(EntityTypeBuilder<{model.model_name}> builder)")
    writer.open_block()
    writer.write_line(model.cs_config_to_table)
    writer.write_blank()
    writer.write_line(model.cs_config_key)
    writer.write_blank()
    for field in model.fields:
        writer.write_hanging_indent(field.cs_config_declarations)
    writer.close_block()
    writer.close_block()


def write_model_data_header(
    writer: cw.CsWriter, namespace: str, class_name: str, entity_namespace: str
) -> None:
    """Write the header for the model data class."""
    writer.write_header()
    writer.write_line(f"using {entity_namespace};")
    writer.write_blank()
    writer.write_line(f"namespace {namespace};")
    writer.write_blank()
    writer.write_line(f"public static class {class_name}")
    writer.open_block()


def write_model_data_function(
    writer: cw.CsWriter, model: tc.ModelConfig, data: list[tuple[object, ...]]
) -> None:
    """Write test data for the given model."""
    func_name = model.model_name
    if func_name.endswith("Entity"):
        func_name = func_name.removesuffix("Entity") + "Entities"
    func_name = "Get" + func_name
    writer.write_line(f"public static {model.model_name}[] {func_name}()")
    writer.open_block()
    writer.write_line("return [")
    writer.indent()
    for i, row in enumerate(data):
        term = "," if i < len(data) - 1 else ""
        writer.write_line(model.get_initializer_row(row) + term)
    writer.dedent()
    writer.write_line("];")
    writer.close_block()
