# Refactors

Refactors--table and column renames--are defined in dedicated configuration
files (in YAML format). Each refactor file contains a list of one or
more refactors like this:

```yaml
- UniqueId: guid_for_refactor
  Type: { TableRename | ColumnRename }
  TableName: name_of_table_if_renaming_a_column
  OldName: original_name_of_table_or_column
  NewName: new_name_of_table_or_column
- UniqueId: guid_for_another_refactor
  Type: { TableRename | ColumnRename }
  TableName: ...
  OldName: ...
  NewName: ...
```

**All** fields are **required** with the following exception:
The `TableName` object is **required** if type is `ColumnRename`, and it is
optional (and ignored) if the type is `TableRename`.

The `UniqueId` is used to determine whether the refactor has already been
applied to the schema.

