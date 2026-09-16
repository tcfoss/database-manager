# DatabaseManager

## About the Project

DatabaseManager is an open source tool for syncing a database definition, in the form of `CREATE TABLE`,
`CREATE VIEW`, etc., statements in (presumably version-controlled)
flat files, with the structure of a live database server.


## Getting Started

> [!NOTE]
> Everything in this section is geared toward application *users*. If you are interested in contributing, see [below](#reporting-issues-and-contributing).

To run the application, you need the [.NET 10+ runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0/runtime) if you don't already have one.

The simplest way to install it is using [NET Install Manager](https://github.com/tcfoss/net-install-manager). If you don't have it but do have `pipx`, you can install it using

```shell
pipx install net-install-manager
# pipx ensurepath  # NOTE BELOW
ninman install-release tcfoss database-manager
```

**Note**: The `pipx ensurepath` invocation is only needed if you haven't called it before or added `~/.local/bin` to your path manually. On Windows, if you *do* need to call it, you will have to close your shell and open a new one before running the next command.

To verify success, run

```shell
dbman --help
```

You should see a description of the available commands.

For more information, see the [App Documentation](docs/App/README.md).


### Other Installation Methods

If you do not want to install `ninman`, you can also directly download the compiled application:

1. Go to the [latest release page](https://github.com/tcfoss/database-manager/releases/latest).
2. Download the `.tar.gz` file that corresponds to your operating system, and unpack it. Move the contents to some reasonable directory.
3. Create a link to the `TcfOss.DatabaseManager.App` (+ `.exe` on Windows) executable in some place on your system `PATH`, or create a launcher script that invokes it.

You can alternatively download the source code and build it yourself (then create a link or launcher script as above).

## Reporting Issues and Contributing

If you want to contribute to this app, start with the [Development Documentation](docs/Development/README.md).

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
