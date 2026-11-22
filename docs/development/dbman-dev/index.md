# The DbMan-Dev Project

Under `tools/dbman-dev` is a Poetry-based Python project for doing assorted routine development activities. If you're unfamiliar with Poetry, see [Using Poetry and pipx](./poetry.md).

The project defines several scripts.


## Entity Framework Code Generation

Entity framework models, configurations, and unit-test data are auto-generated.
From `tools/dbman-dev`, run:

```sh
poetry run dbman-dev-codegen DIALECT [SCHEMA ...] [--skip-models | --skip-data]
```

`DIALECT` is `mysql`, `mariadb`, or `postgres`.

The command regenerates entities and configurations (if `--skip-models` is not specified)
and populates a static class used to hydrate an in-memory database for unit testing (if
`--skip-data` is not specified).

The two `--skip` flags cannot be specified together.

`[SCHEMA ...]` is a space-separated list of schemas to process. Only programmatically registered
schemas are accepted (anything unknown will exit with an error). To regenerate data for *all*
schemas registered for the given dialect, omit the argument. (Since all test data for a given
dialect live in the same file, omitting the argument is recommended except for temporary
diagnostic purposes.)
