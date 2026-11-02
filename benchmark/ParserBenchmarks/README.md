# Parser Benchmarks

MySQL benchmarks against the actual provider projects. Each operation processes
all SQL inputs in the corpus once. File reading and parser/lexer construction are
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

`LoadQuotedIdentifierTestData` adds six targeted inputs covering all three quote
styles accepted by the lexer, doubled and trailing escaped closing delimiters,
empty identifiers, LF/CRLF inside identifiers, Unicode, 64-character names, and
quoted routine labels. These are lexer/parser edge cases, not server-validated
MySQL scripts. Unterminated and embedded-NUL cases belong in unit tests rather
than this successful-parsing workload.

`LoadEscapedStringTestData` adds eight targeted inputs containing ordinary and
national strings, doubled-quote runs, trailing doubled quotes, backslash-escaped
quotes, consecutive backslashes, empty/Unicode/multiline content, lengths around
the 256-character stack-buffer boundary, and cases that exercise initial pool
rental and repeated buffer growth. Escapes remain verbatim in lexer token values;
these inputs pin down existing behavior rather than normalize SQL escaping.
It also includes 1,024- and 4,096-character plain strings and long LF/CRLF
strings to measure bulk scanning separately from escape handling. Malformed
strings and hex-literal edge cases are covered by unit tests instead.

Setup logs token-based quoted-identifier and string coverage outside measured operations.
Use the same expanded corpus for both baseline and candidate runs; results from
before these targeted inputs were added are not directly comparable.

To switch machines or corpora, add a function returning `List<string>` and change
the `_sql = LoadLinuxTestData();` line in `Setup`. Also change the artifact root
in `Program.cs` when moving to Windows.