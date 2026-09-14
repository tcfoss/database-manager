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
