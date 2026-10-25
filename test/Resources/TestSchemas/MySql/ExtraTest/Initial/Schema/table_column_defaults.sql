CREATE TABLE table_column_defaults (
    id INT PRIMARY KEY,
    base_col VARCHAR(20) NOT NULL DEFAULT 'default',
    ref_quoted VARCHAR(20) NOT NULL DEFAULT (`base_col`),
    ref_unquoted VARCHAR(20) NOT NULL DEFAULT (base_col),
    ref_func VARCHAR(20) NOT NULL DEFAULT (LOWER(`base_col`)),
    other_type VARBINARY(20) NOT NULL DEFAULT 'default_secret',
    string_looks_like_func VARCHAR(20) NOT NULL DEFAULT 'lower(`base_col`)'
);
