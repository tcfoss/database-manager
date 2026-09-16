# DatabaseManager Basic Usage

<!-- @import "[TOC]" {cmd="toc" depthFrom=1 depthTo=6 orderedList=false} -->

DatabaseManager lets you specify a database definition in version-controlled
files and use those files to update a live RDBMS.


## Definitions of Terms

There are many programs for managing database data, and they all use terms
somewhat differently. For the purposes of DatabaseManager, I try to pick terms
that can be used _consistently_, even if those terms might be "wrong" in the
contects of one application/dialect or another.

Here is what is meant throughout this documentation (and the application)
by various terms:

RDBMS (Relational Database Management System)
: The database server installed and running on some computer. You can make a connection
to an RDBMS and run queries against it.

Catalog
: A **catalog** is the highest hierarchical level of data organization within an RDBMS.
: This corresponds to a "database" in PostgreSQL and Microsoft SQL Server. (PostgreSQL
also uses the term "catalog" as we're using it here.) Currently, a MySQL or MariaDB
installation only permits _one_ catalog.

Schema
: A **schema** is the next level of hierarchical organization after a Catalog.
: PostgreSQL and Microsoft SQL Server use the term in the same way. MySQL and
MariaDB also do, but it is more common for them to refer to it as a "database".

Object
: An **object**, or **database object** is a top-level member of a Schema.
: Objects include tables, views, stored procedures, stored functions, and triggers.
In MySQL/MariaDB, events are also objects.
: Even though some RDBMSs treat indexes as top-level members, DatabaseManager
will not — an index remains a child of a table (or, in some RDBMSs, a view).

Database Definition
: A **database definition** (or sometimes just **definition**) is the complete set
of database objects, grouped into schemas, under a catalog. It's a complete
specification of the structure of the catalog.
: In the context of the `compute-changes` command (coming soon), there are two
database definitions of note:

1. The database definition specified by a set of files on your filesystem.
2. The database definition already active on your installed RDBMS.

The purpose of `compute-changes` is to generate a sequence of statements
that will bring definition (2) into line with definition (1).


## The `compute-changes` Command

| Asset               | Required |
| ------------------- | :------: |
| Configuration file  |   yes    |
| Non-generic dialect |   yes    |
| RDBMS connection    |   yes    |

The `compute-changes` command is invoked as follows:

```sh
dbman compute-changes [output_path] [--file-exists-action|-f { error | rename | overwrite | skip }]
```

It constructs two database definitions:

1. One by parsing SQL statements in files on your computer.
2. Another by connecting to a specified RDBMS and examining what's already there.

It then generates a script containing `CREATE`, `ALTER`, and `DROP` statements
which, if piped into your RDBMS (2), will modify your RDBMS to match definition (1).

If `output_path` is omitted, it defaults to `changes.sql`.

The `--file-exists-action` flag determines what happens if `output_path` already
exists. The default action is `rename`.

When this project is more mature, an additional command may be added so that it
actually **executes** those statements, but I don't think it would be wise to
use the output of this program without human review just yet.


## The `format-sql` Command

| Asset               | Required |
| ------------------- | :------: |
| Configuration file  |    no    |
| Non-generic dialect |    no    |
| RDBMS connection    |    no    |

The `format-sql` command is invoked as follows:

```sh
dbman format-sql <files>... [options]
```

It reads one or more SQL files, re-formats each statement according to the
configured formatting rules, and writes the result out.

### Output destination

By default (no `--output-pattern`), each input file is **overwritten in-place**.
A backup of the original file is created automatically unless you pass
`--no-backup` / `-n`.

If you want to write formatted output to a different location, use
`--output-pattern` / `-o` to specify a path template. Use `{fileName}` in the
template as a placeholder for the original file's base name (without extension).
For example:

```sh
dbman format-sql src/*.sql --output-pattern out/{fileName}.formatted.sql
```

The `--file-exists-action` / `-f` flag controls what happens when the output
file already exists. The default is `overwrite`. Other options are `skip`,
`rename`, and `error`.

### Definition-aware formatting

Some formatting features—most notably expanding unqualified column references
into fully-qualified `table.column` form—require knowledge of the database
definition (i.e. which tables exist and what columns they have).

There are two ways to supply this:

1. **A pre-parsed definition file** produced by the `parse-definition` command.
   Pass `--definition-file` / `-d` with the path to that JSON file. This skips
   re-parsing the definition on every run, which can be useful in larger projects.
2. **The configuration file** (see [the next section](#the-configuration-file)).
   If `--definition-file` is not specified but a configuration file is detected,
   the formatter will parse the database definition according to it.

If neither is supplied, the formatter still runs, but any features that depend
on the database definition will be unavailable.


## The Configuration File

The behavior of the program, most importantly how it determines which files to
include when constructing your database definition, is controlled by the contents
of a configuration file `database-manager.yaml` at the root of the filesystem
hierarchy defining your database.

If your configuration file has a non-standard name, or if you are invoking the
application from some location other than the root of your database definition,
you can specify the path at the command line:

```sh
dbman { --config | -c } config_file_path subcommand [options...]
```

The structure of the configuration file is described [here](./ConfigurationFile.md).


## Other Commands

### The `download-schema` Command

| Asset               | Required |
| ------------------- | :------: |
| Configuration file  |   yes    |
| Non-generic dialect |   yes    |
| RDBMS connection    |   yes    |

The `download-schema` command inspects the RDBMS and constructs a database
definition from it. It then saves files on your computer to represent that
definition.

It does not accept any arguments, so the invocation is simply

```sh
database-manager download-schema
```

### The `parse-files` Command

| Asset               | Required |
| ------------------- | :------: |
| Configuration file  |    no    |
| Non-generic dialect |    no    |
| RDBMS connection    |    no    |

Parses the specified SQL files and outputs an abstract syntax tree (AST) representing the
statements therein in JSON.

### The `parse-definition` Command

| Asset               | Required |
| ------------------- | :------: |
| Configuration file  |   yes    |
| Non-generic dialect |   yes    |
| RDBMS connection    |    no    |

Parses your SQL files into a database definition and outputs an AST in JSON. Instead
of the AST representing _statements_, like the output of `parse-files`, the output
of `parse-definition` represents a collection of _database objects_, like tables,
procedures, functions, and views.

The resulting definition JSON can be used as an input to the `format-sql` command.
