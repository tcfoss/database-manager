#!/usr/bin/env bash
set -euo pipefail

export PGPASSWORD="${POSTGRES_PASSWORD:-password}"
psql_args=(--host=127.0.0.1 --username="${POSTGRES_USER:-postgres}" --dbname="${POSTGRES_DB:-postgres}" --set=ON_ERROR_STOP=1)
psql "${psql_args[@]}" <<'SQL'
CREATE ROLE admin LOGIN PASSWORD 'password';
CREATE SCHEMA library_catalog;
CREATE SCHEMA library_identity;
CREATE SCHEMA library_activity;
SQL

for script in \
    LibraryCatalog/Tables/book.sql \
    LibraryCatalog/Tables/contributor.sql \
    LibraryCatalog/Tables/genre.sql \
    LibraryCatalog/Tables/book_author.sql \
    LibraryCatalog/Tables/book_genre.sql \
    LibraryCatalog/Tables/book_search.sql \
    LibraryCatalog/Tables/accidental_table.sql \
    LibraryIdentity/Tables/patron.sql \
    LibraryIdentity/Tables/employee.sql \
    LibraryIdentity/Tables/site_user.sql \
    LibraryActivity/Tables/active_rental.sql \
    LibraryActivity/Tables/rental_log.sql \
    LibraryCatalog/Triggers/genre.sql \
    LibraryCatalog/Triggers/book.sql \
    LibraryCatalog/Triggers/book_author.sql \
    LibraryCatalog/Triggers/book_genre.sql \
    LibraryActivity/Triggers/active_rental.sql \
    LibraryActivity/Views/available_books.sql \
    LibraryActivity/Functions/get_late_charge.sql \
    LibraryActivity/Functions/search_available_books.sql \
    LibraryIdentity/Functions/deactivate_stale_users.sql \
    library_test_data.sql
do
    psql "${psql_args[@]}" --file="/InitialSchemas/${script}"
done

psql "${psql_args[@]}" <<'SQL'
CREATE TABLE library_catalog._database_manager (entry_key CHAR(36) NOT NULL, entry_type CHAR(1) NOT NULL, UNIQUE (entry_key));
CREATE TABLE library_identity._database_manager (entry_key CHAR(36) NOT NULL, entry_type CHAR(1) NOT NULL, UNIQUE (entry_key));
CREATE TABLE library_activity._database_manager (entry_key CHAR(36) NOT NULL, entry_type CHAR(1) NOT NULL, UNIQUE (entry_key));
INSERT INTO library_catalog._database_manager (entry_key, entry_type) VALUES
    ('00000000-0000-0000-0000-000000000001', 'R'),
    ('00000000-0000-0000-0000-000000000003', 'D');
INSERT INTO library_identity._database_manager (entry_key, entry_type) VALUES
    ('00000000-0000-0000-0000-000000000004', 'D');
INSERT INTO library_activity._database_manager (entry_key, entry_type) VALUES
    ('00000000-0000-0000-0000-000000000002', 'R');
SQL
