# Parser Benchmarks

MySQL benchmarks against the actual provider projects. Each operation processes
all SQL files in the corpus once. File reading and parser/lexer construction are
outside the measured operation.

- `EndToEnd`: tokenize, convert the token list to an array, and parse.
- `LexerOnly`: tokenize the SQL strings.
- `ParserOnly`: parse token arrays prepared in `GlobalSetup`, with fresh parser
  state on every call.

All three return counts to keep their results observable. MemoryDiagnoser reports
allocations per corpus pass. These are distinct workloads, not baseline/candidate
implementations; compare the same method between revisions.

## Run

From the repository root, validate that the entire corpus parses successfully:

```sh
dotnet run -c Release --project benchmark/ParserBenchmarks -- --validate
```

Run all benchmarks:

```sh
dotnet run -c Release --project benchmark/ParserBenchmarks -- --filter '*'
```

For a quick harness smoke test, add `--job Dry`. Dry results are not useful for
performance comparisons. To run only one workload, use e.g. `--filter '*EndToEnd*'`.

Logs and reports go to timestamped subdirectories of
`/home/thomas/Desktop/ParserBenchmarks`. Timestamps use UTC. Preserve the reports
and record the source revision when comparing branches. Keep the corpus, SDK,
runtime, benchmark configuration, and machine conditions unchanged.

## Change the Corpus

`LoadLinuxTestData` recursively reads all `*.sql` files in the Library Initial and
Sakila directories in deterministic path order. Files are parsed independently,
with no preprocessing or exclusions; unsupported syntax fails the run.

To switch machines or corpora, add a function returning `List<string>` and change
the `_sql = LoadLinuxTestData();` line in `Setup`. Also change the artifact root
in `Program.cs` when moving to Windows.