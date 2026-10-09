# DatabaseManager

[![codecov](https://codecov.io/gh/tcfoss/database-manager/branch/master/graph/badge.svg?token=NK2W9ZRKRW)](https://codecov.io/gh/tcfoss/database-manager)

## About the Project

DatabaseManager is an open source tool for syncing a database definition, in the form of `CREATE TABLE`, `CREATE VIEW`, etc., statements in (presumably version-controlled) flat files, with the structure of a live database server.

> [!NOTE]
> Support for MySQL and MariaDB is mostly complete—any parsing failures at this stage should be reported as bugs. SQL Server support is *currently* limited to parsing and formatting, and even that is not entirely complete. I hope to eventually add support for PostgreSQL as well.

Documentation: [App usage](https://tcfoss.github.io/database-manager/app/),
[for developers](https://tcfoss.github.io/database-manager/libraries/), and
[contributing](https://tcfoss.github.io/database-manager/development/).


## Getting Started

See the [documentation](https://tcfoss.github.io/database-manager/) for  instructions on installing and using DatabaseManager.


## Using the .NET Packages

The Core, MySQL, MariaDB, and SQL Server libraries are available as NuGet packages targeting .NET 10. Convenience `LibWrapper` packages handle configuration, logging, and dependency-injection startup. See the
[package-selection guide](https://tcfoss.github.io/database-manager/libraries/) and [quick start](https://tcfoss.github.io/database-manager/libraries/quickstart/).


## Reporting Issues and Contributing

If you want to contribute to this app, start with the [contributor guide](https://tcfoss.github.io/database-manager/development/).

If you find bugs or have feature requests, please report them. You can do so on GitHub, though the "source of truth" for this project is [IssueTracker](https://issues.tcflanagan.net/database-manager). It would create slightly less work for me if you reported them there instead. You can log in via GitHub.

Contributions are welcome. Follow the usual fork-and-pull request workflow. Before submitting a pull request, make sure

1. Code is linted: run `dotnet format --severity info --verify-no-changes` in the repo root.
2. All existing tests succeed.
3. Any new code includes appropriate tests.
4. Documentation is updated as necessary (and—especially if AI generates the updates—spaces are removed around any em-dashes).


## License

This project is distributed under the [MIT License](LICENSE).

Copyright (C) 2026 Thomas C. Flanagan.


## Acknowledgements

The SQL parser at the heart of this application is based in large
part on the [SqlParser-cs](https://github.com/TylerBrinks/SqlParser-cs)
project [Copyright (C) 2023 Tyler Brinks and other contributors], also
distributed under the MIT License.

Said project provides a broad SQL parser with support for many
dialects. While planning this project, I considered simply using it
directly as a dependency, but the MySQL/MariaDB DDL-specific features
required for this project were beyond its current scope. Hence, I
adapted and incorporated much of the essential parser logic into
this project, as well as a few of the design details that I
particularly liked (the InterpolatedStringHandler for efficiently printing SQL and nested classes as pseudo-namespaces, in particular).

SqlParser-cs remains a much broader SQL parser than this project aims
to be. If you are looking for a general-purpose SQL parsing library,
I highly recommend checking it out.
