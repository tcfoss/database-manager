Scripts for code generation and other tasks.

## INFORMATION_SCHEMA Test Data

The test-data generator initializes schema projects sequentially in one fresh
database container, then extracts their combined INFORMATION_SCHEMA data into one
C# class per dialect. The default projects are:

| Project | Database Schemas | Initialization Script |
| --- | --- | --- |
| Library | library_catalog, library_identity, library_activity | initialize_library.sh |
| ExtraTest | extra_test | initialize_db.sh |

Both MySQL and MariaDB use the projects under
`test/Resources/TestSchemas/MySql` at the repository root. Each project's `Initial`
directory is staged at `/InitialSchemas` in turn, with scripts executed from
`/InitialSchemas/Init`. Only the staged files are replaced between projects; the
initialized databases remain available until extraction finishes.

```bash
# Both projects, with output outside the checked-in fixtures:
poetry run dbman-dev-infoschema-test-data MySql --output-path /tmp/dbman-mysql-combined.cs
poetry run dbman-dev-infoschema-test-data MariaDb --output-path /tmp/dbman-mariadb-combined.cs

# Select one project:
poetry run dbman-dev-infoschema-test-data MySql --projects ExtraTest --output-path /tmp/dbman-extra.cs

# Explicit selection also determines initialization order:
poetry run dbman-dev-infoschema-test-data MySql --projects Library ExtraTest --output-path /tmp/dbman-both.cs
```

Docker must be available, with port 3309 and container name `mysql_container` free.

### Adding A Project

Add a `ProjectConfig` entry to `get_default_projects()` in
[test_data.py](src/dbman_dev/infoschema_building/test_data.py). This registers it
for CLI selection and includes it in the default run. Its fields are:

- `name`: unique project name used by `--projects` and error messages.
- `source_path`: directory whose contents are uploaded, normally a project's `Initial` directory.
- `schemas`: database names to include in INFORMATION_SCHEMA extraction.
- `initialization_scripts`: script paths relative to `source_path / "Init"`, in execution order.

For programmatic use, pass `projects=[ProjectConfig(...), ...]` to
`get_arguments()`, or set `ModelDataLoaderConfig.projects` directly.
An empty project list is an error, rather than a request for the defaults.
Projects must have distinct names and schema names, existing source directories,
and existing initialization scripts. Shared setup, such as database-user creation, must
tolerate earlier projects having performed it already.

ExtraTest stores its replica-disabled event with `DISABLE ON SLAVE`. Its
initialization script queries the server version, loads that SQL unchanged on
MariaDB, and translates the clause to `DISABLE ON REPLICA` for MySQL.

The supported images are configured in
[defaults.py](src/dbman_dev/db/defaults.py).

### Verification And Fixture Migration

```bash
poetry run python -m unittest discover -s tests -p 'test_model_data_loader.py' -v
```

The focused tests cover configuration, CLI selection, shared-container ordering,
metadata filtering, initialization failures, and both event dialect branches.
Live verification uses the temporary-output commands above and requires Docker.
