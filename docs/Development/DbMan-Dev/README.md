# The DbMan-Dev Project

Under `tools/dbman-dev` is a Poetry-based Python project for doing assorted routine development activities. If you're unfamiliar with Poetry, see [Using Poetry and pipx](./PyPoetry.md).

The project defines several scripts.


## Entity Framework Code Generation

There are three scripts for auto-generating Entity Framework code. They all require that
you have Docker installed.

Calling

```sh
poetry run dbman-dev-infoschema-entities DIALECT
```

where `DIALECT` is either "MySql" or "MariaDb", regenerates the Entity Framework
models, configurations, and context for the respective project.

```sh
poetry run dbman-dev-infoschema-test-data DIALECT
```

where `DIALECT` has the same meaning as above, recreates the mock data used by the
respective project's unit tests.

```sh
poetry run dbman-dev-infoschema-regenerator
```

is the same as running both of the above commands for both "MySql" and "MariaDb".

## Local Installation

To install the application, run

```sh
poetry run dbman-dev-install install [--system]
```

This does the following:

1. Builds and publishes TcfOss.DatabaseManager.App.
2. "Installs" the results of the publish.

The meaning of "install" changes depending on whether the `--system` flag is set.

If `--system` is set, then "install" means

1. Copy the files to `/usr/local/lib/tcf-database-manager`.
2. Create a symlink to the primary executable in `/usr/local/bin` with the name `dbman`.

Otherwise, "install" means

1. Copy the files to `~/.local/lib/tcf-database-manager`.
2. Creates a symlink to the primary executable in `~/.local/bin` with the name `dbman`.
