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
Run integration tests only with the required Docker environment available:

```sh
dotnet test --solution solutions/IntegrationTest.slnx
```

Package consumers should use the [library guide](../libraries/index.md) rather
than these contributor instructions.


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

The preview runs at `http://127.0.0.1:8000/database-manager/`. Write internal page links as relative
Markdown paths, including fragments when linking to headings. Link repository
files outside `docs/` using their GitHub URLs.

Pull requests build the site without deploying in the existing PR workflow.
The release workflow builds the CLI, NuGet packages, and documentation from the
same `vX.Y.Z` tag. Pages deployment waits for successful release finalization
and occurs only for the highest published stable version. Ordinary merges to
`master` do not deploy documentation; prereleases and re-releases of older
versions do not replace the site.

Documentation builds and deployment appear as separate jobs in the release
workflow. If documentation fails to build or deploy, the existing site remains
available; inspect the failed job and rerun it after addressing the cause.
Use `master` when manually running the release workflow.

With Node.js 24 installed, run the release-policy regression tests locally:

```sh
node --test .github/scripts/docs-release.test.cjs
```


## Feedback and Pull Requests

Report bugs and feature requests on
[IssueTracker](https://issues.tcflanagan.net/database-manager).
See the [repository contribution checklist](https://github.com/tcfoss/database-manager#reporting-issues-and-contributing)
before opening a pull request.
