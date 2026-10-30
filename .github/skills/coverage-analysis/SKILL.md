---
name: coverage-analysis
description: 'Use when analyzing OpenCover or Cobertura XML coverage reports, merging test-project coverage, finding missed branches or zero-hit lines, comparing coverage runs, or tracking uncoverable branches with ldev-coverage.'
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

## DatabaseManager Workflow

- Refresh reports with `dbman-dev-coverage --solution solutions/BasicTests.slnx`.
   This runs unit tests and the configured lightweight/containerless integration
   tests, writes a timestamped report, and refreshes `test-results/latest`.
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