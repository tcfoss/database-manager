# DatabaseManager

## About the Project

DatabaseManager is an open source tool for synching a database definition in the form of `CREATE TABLE`,
`CREATE VIEW`, etc., statements in (presumably version-controlled)
flat files with the structure in a live database server.

## Getting Started

This will be expanded eventually. For now, see the [App Documentation](docs/App/index.md).

For developing the application see the [Development Documentation](docs/Development/index.md).


## Reporting Issues and Contributing

Issues for this project are tracked on [IssueTracker](https://issues.tcflanagan.net/database-manager). If you encounter any bugs or have feature requests, please submit them there.

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
