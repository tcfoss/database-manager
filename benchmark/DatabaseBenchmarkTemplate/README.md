# Benchmark Template

This project contains a template for setting up benchmarks using BenchmarkDotNet to compare
performance of different implementations. By default it loads the MariaDB dependencies.

## Usage

```shell
cd benchmark
dotnet new install <path-to-template-directory>
```

```sh
dotnet new tcfoss-db-benchmark -n MyBenchmarkProject
```

The generated project is intended to live under this repository's `benchmark/` directory.
Its project reference expects that layout so the benchmark can use the current source code.
If you create the project elsewhere, update the `ProjectReference` in the generated `.csproj`
to point to this repository's `src/LibWrapper/TcfOss.DatabaseManager.MariaDb.LibWrapper/`
project.

Before running the benchmark, fill in the project directory, schema, and database credentials
in `AppStarter.cs`. Do not commit real credentials.
