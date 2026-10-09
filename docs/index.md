---
description: Documentation for DatabaseManager CLI users, .NET package consumers, and contributors.
---

# DatabaseManager

DatabaseManager compares SQL database definitions stored in version-controlled files
with the objects on a live server and generates SQL scripts to reconcile them.
It also parses and formats SQL files.

!!! warning
    Because this project is fairly new, you are **strongly** encouraged to review all generated SQL scripts carefully before executing them on a live server, or at least back up your database first.

## Prerequisites

The CLI requires the [.NET 10 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).

## Installing

### Using `ninman`

The simplest way to install DatabaseManager is with [.NET Install Manager](https://github.com/tcfoss/net-install-manager):

```sh
pipx install net-install-manager
# pipx ensurepath (1)
ninman install tcfoss:database-manager
dbman --help
```

1. The `pipx ensurepath` invocation is only needed if you haven't called it before or added `~/.local/bin` to your path manually. On Windows, if you *do* need to call it, you will have to close your shell and open a new one before running the next command.


!!! tip
    Using `ninman` to install DatabaseManager will also make it upgradable via `ninman upgrade dbman`.


### Release Artifact

1. Go to the [latest release page](https://github.com/tcfoss/database-manager/releases/latest).
2. Download the `.tar.gz` file that corresponds to your operating system, and unpack it. Move the contents to some reasonable directory.
3. Create a link to the `TcfOss.DatabaseManager.App` (+ `.exe` on Windows) executable in some place on your system `PATH`, or create a launcher script that invokes it.

### Compile from Source

You can also clone the repository and compile DatabaseManager from source. Here are the general steps:

1. Clone the repository:

    ```sh
    git clone https://github.com/tcfoss/database-manager.git
    ```

2. Build the project using the .NET SDK:

    ```sh
    cd database-manager
    dotnet publish -c Release -o ./publish src/TcfOss.DatabaseManager.App/TcfOss.DatabaseManager.App.csproj
    ```

3. The compiled executable will be located at `publish/TcfOss.DatabaseManager.App` (+ `.exe` on Windows). Create a link to it or add this directory to your system `PATH`.


## Getting Started

Start with the [CLI guide](app/index.md), then configure your project using the
[configuration reference](app/configuration.md).


## .NET Package Consumers

Use the [.NET libraries](libraries/index.md) to embed parsing, formatting, or
MySQL/MariaDB definition comparison in your own application. Start with the
[offline quick start](libraries/quickstart.md), or use
[manual hosting](libraries/customization.md#manual-hosting-without-libwrapper)
when your application owns its dependency-injection container or you'd prefer to `new`-up objects directly.

## Contributors

The [contributor guide](development/index.md) covers building, testing, documentation,
and development tools. Report bugs and feature requests on
[IssueTracker](https://issues.tcflanagan.net/database-manager).

## License and Source

DatabaseManager is distributed under the
[MIT license](https://github.com/tcfoss/database-manager/blob/master/LICENSE).
See the [source repository](https://github.com/tcfoss/database-manager) and
[third-party notices](https://github.com/tcfoss/database-manager/blob/master/THIRD-PARTY-NOTICES.md),
including the [Apache 2.0 license](ThirdPartyLicenses/Apache-2.0.txt).

