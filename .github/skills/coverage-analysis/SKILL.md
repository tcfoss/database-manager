---
name: coverage-analysis
description: 'Use when analyzing OpenCover or Cobertura XML coverage reports, merging test-project coverage, finding missed branches or zero-hit lines, comparing coverage runs, projecting structured results with jq, or tracking uncoverable branches with ldev-coverage.'
---

# Coverage Analysis

Use the installed `ldev-coverage` CLI from the standalone `local-dev-tools`
package. Do not generate one-off Python/XML parsing scripts for operations the
CLI supports. This skill assumes the executable is already on PATH.

## Repository Settings

- Most recent test results are in `test-results/latest`
- To regenerate coverage reports, run `dbman-dev-coverage --solution solutions/BasicTests.slnx`. This will generate coverage based on unit tests and lightweight (i.e. containerless) integration tests. It will create a report in `test-results/<timestamp>_<solution-name>/` and copy it to `test-results/latest`.
- Prefer OpenCover for exact branch identity.

## Procedure

1. Start with `ldev-coverage --help` and command-specific `--help` to verify the
   installed API. Confirm the selected report run and source revision; do not
   merge baseline, current, and stale runs accidentally.
2. Run `ldev-coverage summary test-results/latest --root "$PWD"` from the
   repository root. A directory containing both formats automatically uses
   OpenCover without double-counting Cobertura. For several projects use
   explicit same-build input files or a quoted glob such as
   `'artifacts/**/coverage*.xml'`.
3. Use `ldev-coverage uncovered test-results/latest --root "$PWD" --format json` for
   structured analysis, or `--details` for concise text with exact branch
   identities. Narrow with `--file 'src/*'`, `--class '*Mapper*'`, `--method
   '*Parse*'`, or `--line 30`. JSON is untruncated unless `--limit` is specified.
4. Read the owning source and nearby tests. Source lines can be covered while
   some outcomes remain missed. Do not infer the meaning of a reported `path`
   number from its value alone. Use a focused test to establish the condition.
5. After changes, run the focused check first. Generate fresh reports with the
   repository's approved command when needed. The CLI itself does not run tests
   or modify files. Keep baseline and current reports separate.
6. Compare with `ldev-coverage compare --baseline before.xml --current after.xml
   --root "$PWD" --format json`. Compare both counts and denominators. Across
   changed builds, outcomes may be added/removed rather than matched because
   source lines or IL offsets shifted. Never merge incompatible branch layouts
   to manufacture an improvement.
7. For a missed defensive branch, trace the full caller path and provider
   option/grammar constraints before deciding it is unreachable. If callers
   cannot supply the rejected state, remove the dead check rather than inventing
   an artificial test. For reachable behavior, add a focused test that names
   the input and expected outcome. Record candidate uncoverable outcomes in an
   existing repo ledger with file/member, source line, ordinal/offset/path when
   available, rationale, supporting evidence, and revision/date; do not create
   a ledger solely to improve a percentage. Never exclude branches only to
   raise coverage.
8. Report verified test results, totals, format/merge limitations, and remaining
   gaps. Cobertura only exposes aggregate branch counts here and cannot safely
   merge separate reports or identify which outcome became covered.

Exit statuses are 0 for successful analysis, 1 for an unmet `--fail-under`
threshold, and 2 for input/argument/merge errors. Fix the selected inputs when a
merge is rejected; do not bypass the tool with an ad hoc parser.

## Structured Queries

Prefer `--jq FILTER` for compact structured analysis. It sends the command's JSON
result through the installed `jq`, automatically selecting JSON on all three
commands. It is equivalent to `--format json | jq FILTER`, without requiring a
shell pipeline. Explicit `--format json` or `--format text` also works; the
projection still receives JSON. Quote the filter to protect it from the shell.

```sh
ldev-coverage summary test-results/latest --root "$PWD" --jq '.files[] | {file, branches, lines}'
ldev-coverage uncovered test-results/latest --root "$PWD" --file 'src/*/Common/PgStringDecoders.cs' --jq '{summary, uncovered_branches, uncovered_lines, warnings}'
ldev-coverage compare --baseline test-results/<before-run>/OpenCover.xml --current test-results/latest/OpenCover.xml --root "$PWD" --group-by file --jq '{baseline, current, groups, warnings}'
```

- Summary has `summary` totals and a `files` array; each file has `file`,
   `branches`, and `lines`. Counts contain `covered`, `missed`, `total`, and `percent`.
- Uncovered results use `uncovered_branches` and `uncovered_lines`, not
   `missed_branches` or `zero_hit_lines`. Branch groups include file, line, member,
   aggregate counts, and opaque `outcomes`; inspect source before interpreting them.
- Compare exposes `baseline`, `current`, `newly_covered`, `regressed`, `added`,
   and `removed`. `--group-by file` or `member` also supplies `groups`.
- Repeat `--file` for OR filtering. `--class`, `--method`, `--line`, `--details`,
   and `--limit` can be combined with `--jq`. A limit truncates result lists before
   projection, not totals; omit it when counting or aggregating all outcomes.
- Empty projections succeed with no output. Invalid jq syntax/runtime filters
   propagate jq exit codes (verified 3/5). An unmet `--fail-under` still returns 1
   after a successful projection, preserving its output. Keep `warnings` in a
   projection when input selection or merge limitations matter.

## DatabaseManager Workflow

- Refresh reports with `dbman-dev-coverage --solution solutions/BasicTests.slnx`.
   This runs unit tests and the configured lightweight/containerless integration
   tests, writes a timestamped report, and refreshes `test-results/latest`.
- ReportGenerator prints its license in arguments. Redact it when capturing output,
  and preserve failure status with `pipefail`; do not inspect raw logs containing it:

   ```sh
   set -o pipefail && dbman-dev-coverage --solution solutions/BasicTests.slnx 2>&1 | sed -E 's/(-license:)[^[:space:]]+/\1[REDACTED]/g'
   ```
- Start branch triage with a narrow query, for example:

   ```sh
   ldev-coverage uncovered test-results/latest --root "$PWD" --class '*MsDdlParser*' --details
   ```

- After a focused change, run its tests, regenerate coverage, then use
   `ldev-coverage compare --baseline test-results/<before-run> --current test-results/latest --root "$PWD" --format text --details`.
   Keep the timestamped baseline intact. Report both covered and total branches;
   dead-branch removal may reduce the denominator.
- ReportGenerator's merged OpenCover may zero or renumber IL offsets. Treat path
   numbers as opaque; use source inspection and targeted tests to establish each
   outcome rather than inferring conditions from path order.
- ReportGenerator's summary totals may differ from the CLI's exact OpenCover
   outcome counts. Label the source of percentages and do not mix denominators.
- Core's generic lexer can treat square brackets as identifier quoting. Test
   array normalization through AST construction or the appropriate provider lexer.
   Flattened normalized ASTs are for comparison, not necessarily printable SQL;
   use the non-flattening path for SQL assertions. Unpaired UTF-16 surrogates in
   string-valued `InlineData` are replaced during attribute encoding: pass a char
   and construct the invalid string inside the test instead.

## Phase 2 Checkpoint (2026-11-07)

- Final BasicTests report: `test-results/20261107-223547_BasicTests`, also copied
   to `test-results/latest`. The approved workflow completed successfully.
- CLI totals: 11,764/12,256 branches (492 missed), 28,922/30,493 lines.
   Before cleanup (`20261107-205128_BasicTests`): 11,700/12,266 branches
   (566 missed). Net counts increased by 64 covered outcomes and decreased by
   10 total outcomes after redundant-guard removal. No directly matched regression
   was introduced. No new coverage exclusions.
- Beginning-of-Phase-2 baseline: `test-results/20261106-145812_BasicTests`.
   It has 10,501/10,994 branches (493 missed). Its comparison still flags one
   candidate match in `PgDefinitionBuilder.ValidateDefinition`, line 606, already
   present before cleanup; changed source/IL identities require source review.
- Numeric/Unicode decoding, Unicode token, Core lexer, provider precedence
   managers, and the targeted array/pattern/quantified AST files are branch-covered.
   Remaining recovery-scope misses: `ExpressionParser` 7, `Normalizer` 10,
   and the unknown-value fallback in `PrecedenceManager` 1. These were not excluded
   or claimed unreachable; use a fresh narrow query before further work.
