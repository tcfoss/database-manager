---
description: Build, test, and contribute to DatabaseManager.
---

# Contributing

## Getting started

Install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
The repository's `global.json` selects the supported SDK feature bands.

From the repository root:

```sh
dotnet build DatabaseManager.slnx
dotnet test --solution solutions/UnitTests.slnx
dotnet format --severity info --verify-no-changes
```

See [integration tests](integration-tests.md) for Docker-backed test infrastructure,
[releases](releases.md) for the release process, and the
[development tools](dbman-dev/index.md) for Entity Framework code generation.
If you have Docker installed, you can also run the integration test suite:

```sh
dotnet test --solution solutions/IntegrationTest.slnx
```

Package consumers should use the [library guide](../libraries/index.md) rather
than these contributor instructions.


## Feedback and Pull Requests

Bugs and feature requests can be logged on GitHub. However, the "source of truth" for this project is [IssueTracker](https://issues.tcflanagan.net/database-manager). You can log in via your GitHub account and you should be able to create work items.

If you would like to actively contribute to implementing features or fixing bugs, reach out and I can upgrade your permissions on IssueTracker.

See the [repository contribution checklist](https://github.com/tcfoss/database-manager#reporting-issues-and-contributing)
before opening a pull request.


## Documentation

Create a Python virtual environment and install the pinned documentation tools:

```sh
python3 -m venv .venv
```

On Linux/macOS, activate it with `source .venv/bin/activate`; in Windows
PowerShell, use `.venv\Scripts\Activate.ps1`. Then run:

```sh
python -m pip install -r requirements-docs.txt
mkdocs build --strict
mkdocs serve
```

The preview runs at `http://127.0.0.1:8000/database-manager/`.

With Node.js 24 installed, run the release-policy regression tests locally:

```sh
node --test .github/scripts/docs-release.test.cjs
```
