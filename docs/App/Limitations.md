# Limitations

DatabaseManager intentionally rejects some otherwise-valid SQL constructs in
database definitions. The goal is to keep definitions unambiguous, portable
between dialects, and amenable to reliable diffing by `compute-changes`.

This page lists the constructs that are deliberately not supported. If your
definition uses one of them, DatabaseManager will surface a `Definition Error`
pointing to the offending statement.

## Tables

### Column-level PRIMARY KEY, UNIQUE, and CHECK constraints are not allowed

Constraints must be declared at the table level so that they can be named and
referenced explicitly. Writing `PRIMARY KEY`, `UNIQUE`, or `CHECK (...)` as
part of a column definition is rejected — move the constraint into a separate
table-level clause instead.

```sql
-- Not supported
CREATE TABLE t (
    id INT PRIMARY KEY,
    email VARCHAR(255) UNIQUE,
    age INT CHECK (age >= 0)
);

-- Supported
CREATE TABLE t (
    id INT,
    email VARCHAR(255),
    age INT,
    PRIMARY KEY (id),
    CONSTRAINT uq_t_email UNIQUE (email),
    CONSTRAINT ck_t_age CHECK (age >= 0)
);
```

### All table-level constraints must be named

Every `UNIQUE`, `FOREIGN KEY`, and `CHECK` constraint declared
at the table level must include an explicit `CONSTRAINT <name>` clause.
Anonymous constraints are rejected because RDBMS-generated names are not
stable across deployments and would defeat reliable diffing.

### `CREATE TABLE ... AS SELECT` is not supported

Tables must be defined by an explicit column list. The `AS SELECT` form
derives the column list from a query, which makes the resulting structure
implicit and not reproducible from the definition file alone.

### Tables must have at least one column

A table definition with no columns is rejected.

## Views

### `SELECT *` is not allowed at the top level of a view

The top-level `SELECT` list of a view definition must enumerate its columns
explicitly. A wildcard would make the view's column list depend on the
current shape of the underlying tables, which is not reproducible.

### Expressions and literals must be aliased

Any selected item in a view that is not a plain column reference must have an
explicit alias (`AS <name>`). This guarantees every view column has a stable
name regardless of how the RDBMS would otherwise derive one.

### Each selected column must resolve to a unique name

If two items in a view's `SELECT` list resolve to the same column name, the
view definition is rejected. Add an alias to disambiguate.

### Other unexpected select items

Constructs in a view's top-level `SELECT` list that DatabaseManager cannot
classify as a column reference, an aliased expression, or a literal value
are rejected.

## Events

### Event schedule dates must be literals

The `AT`, `STARTS`, and `ENDS` clauses of an event must be literal date/time
values. Expressions that depend on the current time (or any non-literal
input) are not supported, because they would resolve differently each time
the definition is applied.

## Stored Programs (Views, Procedures, Functions, Triggers, Events)

### `DEFINER` must be explicit

Stored programs must declare `DEFINER = '<user>'@'<host>'` explicitly. Omitting
the clause causes the RDBMS to record the connecting user, which is not
reproducible.

A default value can be specified in the [configuration file](./ConfigurationFile.md).
If values are provided in the configuration, `DEFINER` must be explicitly provided
only in routines where the value should differ from the default.

### `DEFINER = CURRENT_USER` / `CURRENT_ROLE` is not supported

Even when written explicitly, `DEFINER = CURRENT_USER` (or `CURRENT_ROLE`)
resolves at execution time and is therefore non-deterministic. Use a literal
account name instead.

### `SQL SECURITY` must be explicit

Stored programs must declare `SQL SECURITY DEFINER` or `SQL SECURITY INVOKER`
explicitly rather than relying on the server default.

## Triggers

### `PRECEDES` is not supported

Use `FOLLOWS` to specify trigger firing order. `PRECEDES` is rejected because
allowing both forms would let two triggers each describe their position
relative to the other, complicating ordering. Maybe this will one day be relaxed,
but figuring that out is (very) low-priority.

### Trigger firing order must be defined

When two triggers share the same table, timing, and event, their relative
order must be specified using `FOLLOWS`. Triggers that leave the order
implicit are rejected.

## Foreign Keys

### Foreign keys must have an explicit backing index on the referencing columns

A foreign key's referencing columns must be covered by an index that is
declared explicitly in the table definition. DatabaseManager will not rely
on the engine's automatic backing-index behavior for two reasons:

1. **The auto-created index is a separate database object.** In MySQL and
   MariaDB the foreign key constraint and its backing index (conceptually,
   at least) different entities. The name the engine gives the auto-created
   index is not documented and therefore cannot be relied on to remain
   stable across versions. It would be unclear how to map an indexes when
   they are created implicitly.

2. **Dropping a foreign key would silently drop an index.** If the index
   exists only as an implicit side effect of the foreign key, then removing
   the foreign key from a definition file would also remove the index—without
   that change being visible anywhere in the diff. Requiring the
   index to be declared explicitly makes the index's lifecycle independent
   of, and visible alongside, the foreign key's.

### The referenced table must also have a backing index

The referenced columns must be covered by an index (typically a primary key
or unique constraint) on the referenced table.

## String Columns

For columns of character string types, DatabaseManager must be able to
determine both the character set and the collation from the definition (or
the surrounding table/schema defaults).

The following are rejected:

- Neither the character set nor the collation can be determined for a
  string column.
- The character set cannot be determined.
- The collation cannot be determined.
- The specified character set is not a valid character set for the target
  RDBMS.
- The specified collation is not valid for the resolved character set.
