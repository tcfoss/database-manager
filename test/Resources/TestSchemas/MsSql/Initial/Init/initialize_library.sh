#!/bin/bash

export PATH="$PATH:/opt/mssql-tools18/bin"

SQLCMD_SERVER="${MSSQL_SERVER:-localhost,1433}"
SQLCMD_USER="${MSSQL_USER:-sa}"
SQLCMD_PASSWORD="${MSSQL_PASSWORD:-yourStrongPassword123}"

sqlcmd_exec() {
    sqlcmd -C -I -S "$SQLCMD_SERVER" -U "$SQLCMD_USER" -P "$SQLCMD_PASSWORD" -b "$@"
}

sqlcmd_db() {
    sqlcmd_exec -d library_db "$@"
}

# Execute a SQL file, inserting GO before each CREATE statement to ensure
# proper T-SQL batch separation (required for CREATE TRIGGER, VIEW, etc.)
run_sql_file() {
    local file="$1"
    awk '/^CREATE /{print "GO"} {print}' "$file" | sqlcmd_db
}

# Create database
sqlcmd_exec -Q "CREATE DATABASE library_db"

# Create schemas
sqlcmd_db -Q "CREATE SCHEMA library_catalog"
sqlcmd_db -Q "CREATE SCHEMA library_identity"
sqlcmd_db -Q "CREATE SCHEMA library_activity"

# Create tables (dependency order: referenced tables before referencing tables)
sqlcmd_db -i /InitialSchemas/LibraryCatalog/Tables/book.sql
sqlcmd_db -i /InitialSchemas/LibraryCatalog/Tables/contributor.sql
sqlcmd_db -i /InitialSchemas/LibraryCatalog/Tables/genre.sql
sqlcmd_db -i /InitialSchemas/LibraryIdentity/Tables/patron.sql
sqlcmd_db -i /InitialSchemas/LibraryIdentity/Tables/employee.sql
sqlcmd_db -i /InitialSchemas/LibraryCatalog/Tables/book_author.sql
sqlcmd_db -i /InitialSchemas/LibraryCatalog/Tables/book_genre.sql
sqlcmd_db -i /InitialSchemas/LibraryCatalog/Tables/book_search.sql
sqlcmd_db -i /InitialSchemas/LibraryCatalog/Tables/accidental_table.sql
sqlcmd_db -i /InitialSchemas/LibraryIdentity/Tables/site_user.sql
sqlcmd_db -i /InitialSchemas/LibraryActivity/Tables/active_rental.sql
sqlcmd_db -i /InitialSchemas/LibraryActivity/Tables/rental_log.sql

# Create views
run_sql_file /InitialSchemas/LibraryActivity/Views/available_books.sql

# Create functions
run_sql_file /InitialSchemas/LibraryActivity/Functions/get_late_charge.sql

# Create procedures
run_sql_file /InitialSchemas/LibraryActivity/Procedures/search_available_books.sql

# Create triggers
run_sql_file /InitialSchemas/LibraryCatalog/Triggers/book.sql
run_sql_file /InitialSchemas/LibraryCatalog/Triggers/genre.sql
run_sql_file /InitialSchemas/LibraryCatalog/Triggers/book_author.sql
run_sql_file /InitialSchemas/LibraryCatalog/Triggers/book_genre.sql
run_sql_file /InitialSchemas/LibraryActivity/Triggers/active_rental.sql

# Create _database_manager tracking tables
sqlcmd_db -Q "CREATE TABLE library_catalog._database_manager (entry_key CHAR(36) NOT NULL, entry_type CHAR(1) NOT NULL, CONSTRAINT uq_catalog_dm_entry_key UNIQUE (entry_key))"
sqlcmd_db -Q "CREATE TABLE library_identity._database_manager (entry_key CHAR(36) NOT NULL, entry_type CHAR(1) NOT NULL, CONSTRAINT uq_identity_dm_entry_key UNIQUE (entry_key))"
sqlcmd_db -Q "CREATE TABLE library_activity._database_manager (entry_key CHAR(36) NOT NULL, entry_type CHAR(1) NOT NULL, CONSTRAINT uq_activity_dm_entry_key UNIQUE (entry_key))"

# Insert test data
sqlcmd_db -i /InitialSchemas/library_test_data.sql

# Insert tracking entries
sqlcmd_db -Q "INSERT INTO library_catalog._database_manager (entry_key, entry_type) VALUES ('00000000-0000-0000-0000-000000000001', 'R'), ('00000000-0000-0000-0000-000000000003', 'D')"
sqlcmd_db -Q "INSERT INTO library_activity._database_manager (entry_key, entry_type) VALUES ('00000000-0000-0000-0000-000000000002', 'R')"
