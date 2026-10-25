#!/bin/bash

shopt -s expand_aliases

if ! command -v mysql >/dev/null 2>&1; then
    alias mysql='mariadb'
fi

if ! command -v mysql-with-charset > /dev/null 2>&1; then
    alias mysql-with-charset='mysql --user=root --default-character-set=utf8mb4'
fi

mysql-with-charset --execute="CREATE USER 'admin'@'localhost' IDENTIFIED BY 'password'; FLUSH PRIVILEGES;"
mysql-with-charset --execute="GRANT ALL PRIVILEGES ON *.* TO 'admin'@'localhost'; FLUSH PRIVILEGES;"
mysql-with-charset --execute="GRANT SUPER ON *.* TO 'admin'@'localhost'; FLUSH PRIVILEGES;"

mysql-with-charset --execute="CREATE DATABASE library_catalog"
mysql-with-charset library_catalog < /InitialSchemas/LibraryCatalog/Tables/book.sql
mysql-with-charset library_catalog < /InitialSchemas/LibraryCatalog/Tables/contributor.sql
mysql-with-charset library_catalog < /InitialSchemas/LibraryCatalog/Tables/genre.sql
mysql-with-charset library_catalog < /InitialSchemas/LibraryCatalog/Tables/book_author.sql
mysql-with-charset library_catalog < /InitialSchemas/LibraryCatalog/Tables/book_genre.sql
mysql-with-charset library_catalog < /InitialSchemas/LibraryCatalog/Tables/book_search.sql
mysql-with-charset library_catalog < /InitialSchemas/LibraryCatalog/Tables/accidental_table.sql
mysql-with-charset library_catalog < /InitialSchemas/LibraryCatalog/Triggers/book.sql
mysql-with-charset library_catalog < /InitialSchemas/LibraryCatalog/Triggers/genre.sql
mysql-with-charset library_catalog < /InitialSchemas/LibraryCatalog/Triggers/book_author.sql
mysql-with-charset library_catalog < /InitialSchemas/LibraryCatalog/Triggers/book_genre.sql
mysql-with-charset --execute="CREATE TABLE library_catalog._database_manager (entry_key CHAR(36) NOT NULL, entry_type CHAR(1) NOT NULL, UNIQUE KEY _database_manager_entry_key (entry_key)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;"

mysql-with-charset --execute="CREATE DATABASE library_identity"
mysql-with-charset library_identity < /InitialSchemas/LibraryIdentity/Tables/patron.sql
mysql-with-charset library_identity < /InitialSchemas/LibraryIdentity/Tables/employee.sql
mysql-with-charset library_identity < /InitialSchemas/LibraryIdentity/Tables/site_user.sql
mysql-with-charset library_identity < /InitialSchemas/LibraryIdentity/Events/deactivate_stale_users.sql
mysql-with-charset --execute="CREATE TABLE library_identity._database_manager (entry_key CHAR(36) NOT NULL, entry_type CHAR(1) NOT NULL, UNIQUE KEY _database_manager_entry_key (entry_key)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;"

mysql-with-charset --execute="CREATE DATABASE library_activity"
mysql-with-charset library_activity < /InitialSchemas/LibraryActivity/Tables/active_rental.sql
mysql-with-charset library_activity < /InitialSchemas/LibraryActivity/Tables/rental_log.sql
mysql-with-charset library_activity < /InitialSchemas/LibraryActivity/Triggers/active_rental.sql
mysql-with-charset library_activity < /InitialSchemas/LibraryActivity/Views/available_books.sql
mysql-with-charset library_activity < /InitialSchemas/LibraryActivity/Functions/get_late_charge.sql
mysql-with-charset library_activity < /InitialSchemas/LibraryActivity/Procedures/search_available_books.sql
mysql-with-charset --execute="CREATE TABLE library_activity._database_manager (entry_key CHAR(36) NOT NULL, entry_type CHAR(1) NOT NULL, UNIQUE KEY _database_manager_entry_key (entry_key)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;"

mysql-with-charset library_catalog < /InitialSchemas/library_test_data.sql

mysql-with-charset --execute="INSERT INTO library_catalog._database_manager (entry_key, entry_type) VALUES ('00000000-0000-0000-0000-000000000001', 'R'), ('00000000-0000-0000-0000-000000000003', 'D');"
mysql-with-charset --execute="INSERT INTO library_activity._database_manager (entry_key, entry_type) VALUES ('00000000-0000-0000-0000-000000000002', 'R');"
