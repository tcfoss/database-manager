---
description: Documentation for DatabaseManager CLI users, .NET package consumers, and contributors.
---

# DatabaseManager

DatabaseManager compares SQL database definitions stored in version-controlled files
with the objects on a live server and generates SQL scripts to reconcile them.
It also parses and formats SQL files.

## CLI Users

Start with the [CLI guide](app/index.md), then configure your project using the
[configuration reference](app/configuration.md). Review generated changes before
executing them against a server.

The CLI requires the [.NET 10 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
Install it with [NET Install Manager](https://github.com/tcfoss/net-install-manager):

```sh
pipx install net-install-manager
ninman install tcfoss:database-manager
dbman --help
```

Alternatively, download the archive for your platform from the
[latest release](https://github.com/tcfoss/database-manager/releases/latest).
Extract it and put a launcher or symlink pointing to `TcfOss.DatabaseManager.App` on your `PATH`.

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

