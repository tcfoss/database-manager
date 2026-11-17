# Limitations

DatabaseManager intentionally rejects some otherwise-valid SQL constructs in
database definitions. The goal is to keep definitions unambiguous and amenable to
reliable diffing by `compute-changes`.

This section lists the constructs that are deliberately not supported, arranged by dialect.
If your definition uses one of them, DatabaseManager will report a `Definition Error`
pointing to the offending statement and terminate with exit code 3.

See the page for your specific dialect:

- [MySQL/MariaDB](mysql.md)
- [PostgreSQL](postgres.md)
