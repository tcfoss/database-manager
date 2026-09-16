# Formatting Settings

Two top-level blocks in [the configuration file](./ConfigurationFile.md)
control how DatabaseManager emits SQL:

- `Formatting` — used by the `format-sql` command and any other path that
  reformats user-authored SQL files.
- `DifferFormatting` — used by `compute-changes` when emitting `CREATE`,
  `ALTER`, and `DROP` statements that bring an RDBMS into line with your
  definition files.

Both blocks are optional. Any key you omit takes its built-in default.

## `Formatting`

| Key                                | Type                                | Default    | Description                                                                                                                            |
| ---------------------------------- | ----------------------------------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| `PreferTabs`                       | bool                                | `false`    | Indent with tabs instead of spaces.                                                                                                    |
| `TabSize`                          | int                                 | `4`        | Number of spaces per indent level (and the assumed display width of a tab).                                                            |
| `Quoting`                          | `Always` \| `WhenNeeded` \| `Never` | `Always`   | When to quote identifiers in output. `WhenNeeded` quotes only reserved words and identifiers with special characters.                  |
| `ObjectNamePrefixWithSchema`       | bool                                | `false`    | Qualify object names with their schema (e.g. `my_schema.my_table`).                                                                    |
| `OmitModifiersIfDefault`           | bool                                | `true`     | Drop redundant modifiers (e.g. `DEFAULT NULL` on a nullable column without an explicit default) when they match the dialect's default. |
| `OpeningParensOnNewLine`           | bool                                | `true`     | Place opening parentheses on a new line for multi-line constructs (e.g. `CREATE TABLE`).                                               |
| `ExpandWildcards`                  | bool                                | `false`    | Expand `SELECT *` into the explicit column list. Requires a known database definition.                                                 |
| `SelectItemPrefixWithObject`       | bool                                | `true`     | Qualify columns in `SELECT` lists with their table or alias. Requires a known database definition.                                     |
| `UpdateTargetPrefixWithObject`     | bool                                | `true`     | Qualify the target column in `UPDATE ... SET` with its table or alias. Requires a known database definition.                           |
| `JoinConditionIndent`              | int or null                         | `null`     | Extra indentation (in display columns) applied to `ON` clauses. `null` keeps `ON` aligned with `JOIN`.                                 |
| `SpacesBeforeLineComment`          | int                                 | `2`        | Spaces between trailing code and a `--` line comment on the same line.                                                                 |
| `RoutineParameterMultiLineThreshold` | int or null                       | `3`        | If a routine has at least this many parameters, format them one per line. `null` always keeps them on one line.                        |
| `ValueListMultiLineThreshold`      | int or null                         | `3`        | If a value list (e.g. `INSERT ... VALUES`, `IN (...)`) has at least this many items, format them across multiple lines. `null` keeps them on one line. |

Several keys (`ExpandWildcards`, `SelectItemPrefixWithObject`,
`UpdateTargetPrefixWithObject`) need to know which tables and columns exist
to do their job. See
[Definition-aware formatting](./README.md#definition-aware-formatting) for how
to supply a database definition to the `format-sql` command.

### Example

```yaml
Formatting:
  PreferTabs: false
  TabSize: 2
  Quoting: WhenNeeded
  ObjectNamePrefixWithSchema: true
  OpeningParensOnNewLine: false
  RoutineParameterMultiLineThreshold: 2
```

## `DifferFormatting`

| Key                          | Type | Default | Description                                                                                                                                                  |
| ---------------------------- | ---- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `ObjectNamePrefixWithSchema` | bool | `false` | Qualify object names with their schema in emitted statements.                                                                                                |
| `OmitModifiersIfDefault`     | bool | `true`  | Drop redundant modifiers when they match the dialect's default.                                                                                              |
| `PreferRawText`              | bool | `false` | Shorthand: apply raw-text preference to all object-level `*PreferRawText` flags unless a more specific flag is set.                                        |
| `ProcedurePreferRawText`     | bool | `false` | Emit stored-procedure bodies using the original text from the definition file rather than re-rendering from the parsed AST.                                  |
| `FunctionPreferRawText`      | bool | `false` | Same, for stored functions.                                                                                                                                  |
| `TriggerPreferRawText`       | bool | `false` | Same, for triggers.                                                                                                                                          |
| `ViewPreferRawText`          | bool | `false` | Same, for views.                                                                                                                                             |
| `EventPreferRawText`         | bool | `false` | Same, for events.                                                                                                                                            |
| `PreferRawTextInScript`      | bool | `false` | Shorthand for `*PreferRawTextInScript` flags used when statements come from deploy scripts.                                                                 |
| `ProcedurePreferRawTextInScript` | bool | `false` | Script-specific raw-text preference for procedures. Falls back to `PreferRawTextInScript`, then non-script procedure/global flags if omitted.               |
| `FunctionPreferRawTextInScript` | bool | `false` | Script-specific raw-text preference for functions. Falls back to `PreferRawTextInScript`, then non-script function/global flags if omitted.                 |
| `TriggerPreferRawTextInScript` | bool | `false` | Script-specific raw-text preference for triggers. Falls back to `PreferRawTextInScript`, then non-script trigger/global flags if omitted.                   |
| `ViewPreferRawTextInScript`  | bool | `false` | Script-specific raw-text preference for views. Falls back to `PreferRawTextInScript`, then non-script view/global flags if omitted.                         |
| `EventPreferRawTextInScript` | bool | `false` | Script-specific raw-text preference for events. Falls back to `PreferRawTextInScript`, then non-script event/global flags if omitted.                       |
| `TerminateStatements`        | bool | `true`  | Append a statement terminator (`;` or the active delimiter) after each emitted statement.                                                                    |
| `UseDelimiterAroundPrograms` | bool | `true` | Wrap stored programs in `DELIMITER` directives so the script can be piped directly into `mysql`/`mariadb` clients without splitting on `;`.                  |
| `UseDelimiterAroundViews`    | bool | `false` | Wrap `CREATE VIEW` statements in `DELIMITER` directives. This setting is independent from `UseDelimiterAroundPrograms`.                                      |
| `EmitCommentsWithWeights`    | bool | `false` | Include diagnostic comments showing the diff weights used to order statements. Useful when investigating why `compute-changes` produced a particular order.  |

The `*PreferRawText` settings are useful when you have hand-formatted bodies
(comments, unusual whitespace, etc.) that you do not want the formatter to
rewrite. They have no effect when the original text is unavailable—for
example, when the definition came from a `download-schema` run.

When both shorthand and specific keys are present, the specific key wins.
For deploy scripts, `*PreferRawTextInScript` keys are checked first, then
their non-script equivalents.

### Example

```yaml
DifferFormatting:
  ObjectNamePrefixWithSchema: true
  PreferRawText: false
  ViewPreferRawText: true
  ProcedurePreferRawText: true
  PreferRawTextInScript: true
  ViewPreferRawTextInScript: false
  UseDelimiterAroundPrograms: true
  UseDelimiterAroundViews: false
```
