# DbMan-Dev

Developer tools for generating C# system-table models and test fixture data.

## Registering Test Fixtures

Add a `DbInitialization` entry to `_mysql_projects` or `_pg_projects` in
[projects.py](src/dbman_dev/code_generation/projects.py). MySQL and MariaDB
share the MySQL fixture registrations.

- `source_path`: local fixture directory, normally
  `test/Resources/TestSchemas/<Dialect>/<Fixture>/Initial` beneath the repository root.
  Its contents are copied into the container at `/InitialSchemas`.
- `initialization_scripts`: script paths relative to `source_path / "Init"`.
  They run in order with `/InitialSchemas/Init` as their working directory.
- `provided_schemas`: names of the schemas created by the fixture.

Fixtures are initialized in registration order. The staging directory is replaced
between fixtures, but their database objects remain available for extraction.
Shared setup must tolerate earlier fixtures having performed it already.

Generated output paths are set on `ProjectConfig` in the same module:

- `entity_dir`: destination directory for C# entity files.
- `config_dir`: destination directory for EF configuration files.
- `data_file`: destination C# file containing test-fixture data for an in-memory-backed database context.

Setting an output path to `None` disables that output.

## Query Templates

Query configuration lives on `TableConfig` and `ColumnConfig` in
[table_config.py](src/dbman_dev/code_generation/table_config.py).
The source table is qualified by its system schema and aliased as `t`.

- `ColumnConfig.query_template` replaces a column's projection with a SQL
  expression, such as `t.typinput::oid`.
- `schemas_join` adds a join and can override the schema predicate's left-hand side.
- `additional_predicate` adds an `AND` condition.
- `order_by` supplies ordering expressions; otherwise key columns determine
  ordering. An empty list disables ordering.
- `filter_by_schema=False` removes the fixture-schema restriction for global tables.

Use `data_query_template` to replace the complete query:

```python
data_query_template = """
SELECT {columns} FROM {table}
WHERE {schema_filter}
{order_by}
"""
```

The available substitutions are:

| Token | Expansion |
| --- | --- |
| `{columns}` | Ordered column expressions. Required. |
| `{table}` | Qualified source table, including `AS t`. |
| `{schema_filter}` | Complete predicate restricting rows to fixture schemas. |
| `{schemas}` | Parameter placeholders for fixture schema names. |
| `{order_by}` | Generated `ORDER BY` clause, or an empty string. |

When schema filtering is enabled, include `{schema_filter}` or use `{schemas}`
inside your own predicate. Include `{order_by}` when ordering is generated.
Full-query templates must contain their own joins and additional predicates;
they override `schemas_join` and `additional_predicate`.

SQL fragments are trusted developer-authored SQL; schema values are bound as
driver parameters. Escape literal percent signs as `%%`.
