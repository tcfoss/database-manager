#!/bin/bash

set -e -o pipefail

shopt -s expand_aliases

if ! command -v mysql >/dev/null 2>&1; then
    alias mysql='mariadb'
fi

if ! command -v mysql-with-charset > /dev/null 2>&1; then
    alias mysql-with-charset='mysql --user=root --default-character-set=utf8mb4'
fi

mysql-with-charset --execute="CREATE USER IF NOT EXISTS 'admin'@'localhost' IDENTIFIED BY 'password'; FLUSH PRIVILEGES;"
mysql-with-charset --execute="GRANT ALL PRIVILEGES ON *.* TO 'admin'@'localhost'; FLUSH PRIVILEGES;"
mysql-with-charset --execute="GRANT SUPER ON *.* TO 'admin'@'localhost'; FLUSH PRIVILEGES;"

mysql-with-charset --execute="CREATE DATABASE extra_test"
mysql-with-charset extra_test < /InitialSchemas/Schema/with_desc_index.sql
mysql-with-charset extra_test < /InitialSchemas/Schema/event_at.sql
mysql-with-charset extra_test < /InitialSchemas/Schema/event_disable.sql

server_version=$(mysql-with-charset --batch --skip-column-names --execute="SELECT VERSION()")
if [[ "$server_version" == *MariaDB* ]]; then
    mysql-with-charset extra_test < /InitialSchemas/Schema/event_disable_on_slave.sql
else
    sed 's/^DISABLE ON SLAVE$/DISABLE ON REPLICA/' /InitialSchemas/Schema/event_disable_on_slave.sql \
        | mysql-with-charset extra_test
fi

mysql-with-charset --execute="CREATE TABLE extra_test._database_manager (entry_key CHAR(36) NOT NULL, entry_type CHAR(1) NOT NULL, UNIQUE KEY _database_manager_entry_key (entry_key)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;"

mysql-with-charset --execute="INSERT INTO extra_test._database_manager (entry_key, entry_type) VALUES ('00000000-0000-0000-0000-000000000001', 'R'), ('00000001-0000-0000-0000-000000000001', 'D');"
mysql-with-charset --execute="INSERT INTO extra_test._database_manager (entry_key, entry_type) VALUES ('00000001-0000-0000-0000-000000000002', 'R');"
